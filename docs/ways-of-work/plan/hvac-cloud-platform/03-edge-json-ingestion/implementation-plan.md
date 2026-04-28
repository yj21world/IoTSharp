# Implementation Plan：边缘网关 JSON 接入模块

> 对应 PRD：[边缘网关 JSON 接入模块 PRD](../03-edge-json-ingestion/prd.md)
> 状态：**基础能力已有，待补齐格式规范和诊断能力**

## 1. 实施概述

边缘 JSON 接入主要复用现有 MQTT Controller（`TelemetryController`、`GatewayController`）和 HTTP Controller（`DevicesController`），核心链路已跑通。重点工作是：
1. 审查现有 JSON 处理逻辑，补充错误日志上下文。
2. 补充 HTTP 网关批量上传端点（当前主要在 MQTT 侧实现）。
3. 统一 JSON 解析失败时的错误响应格式。

## 2. 涉及文件

| 优先级 | 文件 | 动作 |
|--------|------|------|
| P0 | `IoTSharp/Services/MQTTControllers/TelemetryController.cs` | **审查** — JSON/XML/Binary 处理完整性 |
| P0 | `IoTSharp/Services/MQTTControllers/GatewayController.cs` | **审查** — JSON 解析错误处理 |
| P1 | `IoTSharp/Controllers/DevicesController.cs` | **审查** — HTTP 遥测上传端点 |
| P1 | `IoTSharp/Controllers/GatewayController.cs` | **可能新建** — HTTP 网关批量上传端点 |
| P2 | `IoTSharp.Data/ProduceDataMapping.cs` | **审查** — 映射规则是否满足 1:1 字段映射 |

## 3. JSON 解析与错误处理审查

### 3.1 TelemetryController 审查要点

`IoTSharp/Services/MQTTControllers/TelemetryController.cs` 约 91 行 `telemetry()` 方法：

```csharp
// 审查项：
// 1. JSON 解析 (application/json) 是否正确处理了空 body
// 2. 嵌套 JSON 对象如何处理（当前是 flatten 还是保持嵌套？）
// 3. 解析失败时的错误日志是否包含设备名和原始数据（截断后）
// 4. 数值类型转换是否健壮（非数值字符串 → Double.Parse 会抛异常）

// 建议增强的错误处理模式：
try
{
    var keyValues = JsonSerializer.Deserialize<Dictionary<string, object>>(payload);
    if (keyValues == null || keyValues.Count == 0)
    {
        _logger.LogWarning("Empty telemetry payload from device {Device}", devname);
        return; // 或返回错误
    }
    await _queue.PublishTelemetryData(device, keyValues);
}
catch (JsonException ex)
{
    _logger.LogWarning(ex, "JSON parse failed for device {Device}, payload: {Payload}",
        devname, payload.Length > 500 ? payload[..500] : payload);
    // 不抛异常 — MQTT 消息已经消费，记录日志即可
}
```

### 3.2 GatewayController 审查要点

`IoTSharp/Services/MQTTControllers/GatewayController.cs` 约 57 行 `telemetry()` 方法：

```csharp
// 审查项：
// 1. ThingsBoard 兼容格式：{ "deviceName": [{ "ts": ..., "values": {...} }] }
//    是否正确处理了 ts 为空的情况（使用服务器接收时间兜底）
// 2. 子设备不存在时的处理策略（当前是自动创建 CreateSubDeviceIfNotExists）
//    这个行为是否应该可选？
// 3. 批量数据中部分设备失败时是否影响其他设备

// 建议增强：
// - 添加配置开关控制"自动创建子设备"
// - 批量处理时使用 try/catch per device，一个设备失败不影响其他
foreach (var kv in gatewayData)
{
    try
    {
        // 处理单个子设备数据
        await ProcessSubDeviceTelemetry(kv.Key, kv.Value);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to process telemetry for sub-device {Device} under gateway {Gateway}",
            kv.Key, gatewayName);
    }
}
```

## 4. HTTP 网关批量上传端点

如果当前 `DevicesController` 中没有网关批量上传的 HTTP 端点，则需要新增：

```csharp
// 在 DevicesController.cs 或新建 GatewayController.cs 中：

/// <summary>
/// 网关批量遥测上传（ThingsBoard 兼容格式）
/// </summary>
[HttpPost("api/gateway/{gatewayName}/telemetry")]
[AllowAnonymous] // 网关认证走中间件或 AccessToken header
public async Task<ApiResult<bool>> GatewayTelemetry(
    string gatewayName,
    [FromBody] Dictionary<string, List<GatewayPlayload>> data)
{
    // 1. 查找网关设备
    var gateway = await _dbContext.Device.OfType<Gateway>()
        .FirstOrDefaultAsync(d => d.Name == gatewayName);
    if (gateway == null)
        return new ApiResult<bool>(ApiCode.NotFound, "Gateway not found");

    // 2. 处理每个子设备的数据（复用 GatewayController 的逻辑）
    int successCount = 0;
    int errorCount = 0;
    foreach (var kv in data)
    {
        try
        {
            var childDevice = await EnsureSubDevice(gateway, kv.Key);
            foreach (var payload in kv.Value)
            {
                var ts = payload.ts > 0 
                    ? DateTimeOffset.FromUnixTimeMilliseconds(payload.ts).UtcDateTime 
                    : DateTime.UtcNow;
                await _queue.PublishTelemetryData(new PlayloadData
                {
                    DeviceId = childDevice.Id,
                    MsgBody = payload.values,
                    DataSide = DataSide.ServerSide,
                    DataCatalog = DataCatalog.TelemetryData,
                    ts = ts
                });
            }
            successCount++;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gateway telemetry upload failed for device {Device}", kv.Key);
            errorCount++;
        }
    }

    return new ApiResult<bool>(ApiCode.Success, true, 
        $"Processed {successCount} devices, {errorCount} errors");
}
```

## 5. 字段映射配置

### 5.1 审查 ProduceDataMapping

```csharp
// IoTSharp.Data/ProduceDataMapping.cs
// 审查项：
// 1. 是否支持 SourceField（JSON 字段名）→ TargetKey（遥测键名）的映射
// 2. 是否有关联的 Produce 或 DeviceTypeProfile ID
// 3. 映射范围（全局 / 产品级 / 设备级）

// 如果当前模型不支持，最小扩展方案：
// - SourceField: string      — JSON 中的字段名
// - TargetKey: string        — 平台遥测键名
// - DeviceTypeProfileId: Guid? — 可选，关联到设备类型模板
```

## 6. 统一错误响应格式

所有 JSON 解析和数据转换相关的错误，统一返回格式：

```json
{
  "code": 400,
  "msg": "JSON parse failed: unexpected token at line 1, column 15",
  "data": null
}
```

对于 MQTT 上传（无法直接返回 HTTP 响应），错误信息记录到结构化日志：

```csharp
_logger.LogWarning(
    "JSON upload failed: Gateway={Gateway}, Device={Device}, Error={Error}, PayloadPreview={Payload}",
    gatewayName, deviceName, ex.Message, payloadPreview);
```

## 7. 前端实施要点

### 7.1 边缘网关配置页面

不新增专门页面，边缘 JSON 上传在网关详情页中体现：
- 网关详情页增加"数据上传记录"Tab，展示最近的 JSON 上传日志。
- 支持查看上传成功/失败次数统计。

## 8. 实施步骤

1. 审查 `TelemetryController` — 增强 JSON 解析错误处理和日志。
2. 审查 `GatewayController` — 确认 ThingsBoard 兼容格式的处理完整性。
3. 如没有 HTTP 网关批量上传端点，在 `DevicesController` 中新增。
4. 审查 `ProduceDataMapping` — 确认 1:1 字段映射支持。
5. 补充结构化错误日志（含设备名和截断后的原始数据）。
6. 端到端测试：网关 HTTP/MQTT 上传单设备 JSON → 验证数据入库；网关上传批量 JSON → 验证各子设备数据入库。

## 9. 不需要改动

- MQTT Topic 路由（`devices/{devname}/telemetry`）— 已工作。
- CAP EventBus 发布逻辑 — 已工作。
- `GatewayPlayload` DTO — ThingsBoard 兼容格式已定义。
- 遥测存储链（CAP → IStorage.StoreTelemetryAsync）— 已工作。
