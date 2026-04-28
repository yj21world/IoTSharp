# Implementation Plan：实时数据与历史数据查询模块

> 对应 PRD：[实时数据与历史数据查询模块 PRD](../06-telemetry-data-query/prd.md)
> 状态：**存储和查询核心已就绪，待补齐查询 API 和权限校验**

## 1. 实施概述

此模块的核心 `IStorage` 接口和 TimescaleDB/InfluxDB 实现已完整。重点工作是审查和补充 HTTP API 端点，确保 Controller 层正确调用 `IStorage` 并返回统一 `ApiResult<T>` 格式。不涉及存储层改动。

## 2. 涉及文件

| 优先级 | 文件 | 动作 |
|--------|------|------|
| P0 | `IoTSharp/Controllers/DevicesController.cs` | **审查/补充** — 遥测查询端点 |
| P1 | `IoTSharp/Controllers/TelemetryController.cs` | **可能新建** — 独立遥测查询 API |
| P2 | `IoTSharp/Dtos/TelemetryDataDto.cs` | **审查** — 确认返回字段完整性 |
| P3 | `ClientApp/` | 实时数据展示和历史趋势图表页面 |

## 3. 审查 DevicesController 遥测查询端点

### 3.1 端点存在性检查

审查 `DevicesController.cs` 中是否已有以下端点：

```csharp
// 需要确认存在的端点：

// 1. 设备全部最新值
GET /api/devices/{id}/telemetry/latest

// 2. 设备指定键最新值
GET /api/devices/{id}/telemetry/latest?keys=temperature,humidity

// 3. 设备历史数据
GET /api/devices/{id}/telemetry/history?keys=k1&begin=2026-04-26&end=2026-04-27&every=1h&aggregate=avg
```

### 3.2 如果缺失 — 补充实现

```csharp
// 在 DevicesController.cs 中补充：

/// <summary>
/// 获取设备遥测最新值
/// </summary>
[HttpGet("{id}/telemetry/latest")]
[Authorize]
public async Task<ApiResult<List<TelemetryDataDto>>> GetTelemetryLatest(
    Guid id, 
    [FromQuery] string? keys = null)
{
    var device = await _dbContext.Device.FindAsync(id);
    if (device == null)
        return new ApiResult<List<TelemetryDataDto>>(ApiCode.NotFound, "Device not found");

    // 校验设备归属
    if (!User.IsInRole("Admin") && !await _justMy.CheckDeviceOwnership(User, id))
        return new ApiResult<List<TelemetryDataDto>>(ApiCode.Forbidden, "Access denied");

    List<TelemetryDataDto> result;
    if (string.IsNullOrEmpty(keys))
    {
        result = await _storage.GetTelemetryLatest(id);
    }
    else
    {
        result = await _storage.GetTelemetryLatest(id, keys);
    }

    return new ApiResult<List<TelemetryDataDto>>(ApiCode.Success, result);
}

/// <summary>
/// 获取设备遥测历史数据
/// </summary>
[HttpGet("{id}/telemetry/history")]
[Authorize]
public async Task<ApiResult<List<TelemetryDataDto>>> GetTelemetryHistory(
    Guid id,
    [FromQuery] string? keys = null,
    [FromQuery] DateTime? begin = null,
    [FromQuery] DateTime? end = null,
    [FromQuery] string? every = null,      // 如 "1.00:00:00" = 1天
    [FromQuery] string? aggregate = null)  // "avg" / "max" / "min" / "sum" / "none"
{
    // 参数校验与默认值
    var device = await _dbContext.Device.FindAsync(id);
    if (device == null)
        return new ApiResult<List<TelemetryDataDto>>(ApiCode.NotFound, "Device not found");

    if (string.IsNullOrEmpty(keys))
        return new ApiResult<List<TelemetryDataDto>>(ApiCode.Error, "keys parameter is required for history query");

    var beginTime = begin ?? DateTime.UtcNow.AddHours(-24);
    var endTime = end ?? DateTime.UtcNow;
    var everyTs = every != null ? TimeSpan.Parse(every) : TimeSpan.Zero;
    var agg = aggregate != null ? Enum.Parse<Aggregate>(aggregate, ignoreCase: true) : Aggregate.None;

    var result = await _storage.LoadTelemetryAsync(id, keys, beginTime, endTime, everyTs, agg);
    return new ApiResult<List<TelemetryDataDto>>(ApiCode.Success, result);
}
```

## 4. 多键并发查询优化

`IStorage.LoadTelemetryAsync` 已在 TimescaleDB 实现中使用 `Task.WhenAll` 做多键并行查询：

```csharp
// TimescaleDBStorage.LoadTelemetryAsync（现有实现）
public async Task<List<TelemetryDataDto>> LoadTelemetryAsync(...)
{
    var keyList = keys.Split(',');
    var tasks = keyList.Select(key => AggregateTelemetryAsync(deviceId, key, ...));
    var dataResults = await Task.WhenAll(tasks);
    results.AddRange(dataResults.SelectMany(data => data));
    return results;
}
```

**确认项**：InfluxDB 实现是否也支持多键并发？当前 `InfluxDBStorage.LoadTelemetryAsync` 在 Flux 查询中通过 `filter(fn: (r) => ...)` 和 `group(columns: ["_field"])` 处理多键，功能等价。

**不需要改动。**

## 5. 返回格式规范

所有遥测查询端点统一返回 `ApiResult<List<TelemetryDataDto>>`：

```json
{
  "code": 200,
  "msg": "Success",
  "data": [
    {
      "keyName": "temperature",
      "dateTime": "2026-04-27T10:30:00Z",
      "value": 24.5,
      "dataType": 3
    },
    {
      "keyName": "temperature",
      "dateTime": "2026-04-27T11:00:00Z",
      "value": 25.1,
      "dataType": 3
    }
  ]
}
```

### 5.1 TelemetryDataDto 字段确认

```csharp
// IoTSharp/Dtos/TelemetryDataDto.cs — 审查现有字段
public class TelemetryDataDto
{
    public string KeyName { get; set; }     // 遥测键名
    public DateTime DateTime { get; set; }   // 时间戳
    public object Value { get; set; }        // 遥测值（类型由 DataType 决定）
    public DataType DataType { get; set; }   // 值类型枚举
    // 建议补充（可选，第一阶段不需要）：
    // public string Unit { get; set; }      // 单位
    // public string DisplayName { get; set; } // 显示名称
}
```

## 6. 查询性能注意事项

### 6.1 TimescaleDB 超表查询

当前已创建的索引：
```sql
-- TimescaleDBStorage.CheckTelemetryStorage() 中自动创建
CREATE INDEX ON "TelemetryData" ("KeyName", "DateTime" DESC);
CREATE INDEX ON "TelemetryData" ("DataSide", "DateTime" DESC);
CREATE INDEX ON "TelemetryData" ("Type", "DateTime" DESC);
```

### 6.2 查询建议

- **最新值查询**：直接查 PostgreSQL `DataStorage` 表，走 `(Catalog, DeviceId, KeyName)` 复合主键，响应时间 < 10ms。
- **历史数据查询**：查 TimescaleDB 超表，利用 `(KeyName, DateTime DESC)` 索引 + hypertable 分区裁剪，10 网关量级下响应时间 < 200ms。
- **聚合查询**：`time_bucket` 原生函数，TimescaleDB 自动利用超表分区做并行扫描。

第一阶段不需要额外缓存或物化视图。

## 7. 前端实施要点

```
ClientApp/src/views/telemetry/
├── DeviceTelemetry.vue       // 设备遥测查看页面（最新值卡片 + 历史趋势图）
├── TelemetryChart.vue        // 可复用的时序列折线图组件（ECharts 或 Chart.js）
├── KeySelector.vue           // 遥测键多选组件
└── TimeRangePicker.vue       // 时间范围选择组件
```

### 7.1 API 调用

```typescript
// api/telemetry.ts
export const getLatest = (deviceId: string, keys?: string) =>
  request.get<ApiResult<TelemetryDataDto[]>>(`/api/devices/${deviceId}/telemetry/latest`, { params: { keys } });

export const getHistory = (deviceId: string, params: HistoryQuery) =>
  request.get<ApiResult<TelemetryDataDto[]>>(`/api/devices/${deviceId}/telemetry/history`, { params });

interface HistoryQuery {
  keys: string;
  begin?: string;
  end?: string;
  every?: string;
  aggregate?: string;
}
```

## 8. 实施步骤

1. 审查 `DevicesController.cs` — 检查 `GetTelemetryLatest` 和 `GetTelemetryHistory` 端点是否存在。
2. 如果缺失，按上文模板补充端点实现。
3. 补充设备归属校验（`IJustMy` 或等价权限检查）。
4. 审查 `TelemetryDataDto` — 确认返回字段满足前端展示需求。
5. 实现前端遥测查看页面和设备历史趋势图表。
6. 端到端测试：上传遥测数据 → 查询最新值 → 查询历史趋势。

## 9. 不需要改动

- `IStorage` 接口和 `TimescaleDBStorage` / `InfluxDBStorage` 实现 — 核心查询逻辑完整。
- TimescaleDB 超表结构和索引 — 已自动创建。
- CAP 事件总线 — 写入路径已工作。
- `DataStorage` 表（`TelemetryLatest` / `AttributeLatest`）— TPH 继承和 UPSERT 逻辑正确。
