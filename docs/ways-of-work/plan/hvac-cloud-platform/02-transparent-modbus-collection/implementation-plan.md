# Implementation Plan：透传网关 Modbus 采集模块

> 对应 PRD：[透传网关 Modbus 采集模块 PRD](../02-transparent-modbus-collection/prd.md)
> 状态：**大部分已完成，待全面测试和补齐**
> 当前进度：核心运行时（`ModbusCollectionService` → `GatewayScheduler` → `BatchMerger` → `ModbusDataParser`）和 `CollectionTaskController` / `CollectionTaskService` 已实现，剩余工作为 Optimize 激活、即时重试、协议文档和测试补齐

## 1. 实施概述

此模块是代码库中实现最完整的部分之一。核心运行时（`ModbusCollectionService` → `GatewayScheduler` → `BatchMerger` → `ModbusDataParser`）已跑通，`CollectionTaskController` / `CollectionTaskService` 已实现 CRUD 和日志查询。剩余重点工作是：
1. 审查和完善已有采集配置 API（`CollectionTaskController` / `CollectionDeviceController` / `CollectionPointController`）。
2. 激活 `BatchMerger.Optimize()`。
3. 实现超时即时重试。
4. 补充采集日志查询 API。

## 2. 涉及文件

| 优先级 | 文件 | 动作 |
|--------|------|------|
| P0 | `IoTSharp/Controllers/CollectionTaskController.cs` | **审查/完善** — 采集任务 CRUD API（已存在） |
| P0 | `IoTSharp/Controllers/CollectionDeviceController.cs` | **审查/完善** — 采集设备 CRUD API（已存在） |
| P0 | `IoTSharp/Controllers/CollectionPointController.cs` | **审查/完善** — 采集点位 CRUD API（已存在） |
| P1 | `IoTSharp/Services/ModbusCollection/GatewayScheduler.cs` | **修改** — 激活 `Optimize()` |
| P1 | `IoTSharp/Services/ModbusCollection/ModbusCollectionService.cs` | **修改** — 超时即时重试 |
| P2 | `IoTSharp/Controllers/CollectionLogController.cs` | **审查/完善** — 采集日志查询 API（已存在） |
| P3 | `ClientApp/` | 采集配置管理前端页面 |

## 3. 采集配置 CRUD API

### 3.1 CollectionTaskController

```csharp
// 新建文件: IoTSharp/Controllers/CollectionTaskController.cs
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CollectionTaskController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly GatewaySchedulerManager _schedulerManager;

    // GET /api/collection-tasks — 列表（含网关注释、状态过滤）
    [HttpGet]
    public async Task<ApiResult<PagedData<CollectionTaskDto>>> GetTasks(
        [FromQuery] Guid? gatewayId,
        [FromQuery] bool? enabled,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    { /* 实现 */ }

    // GET /api/collection-tasks/{id}
    [HttpGet("{id}")]
    public async Task<ApiResult<CollectionTaskDto>> GetTask(Guid id)
    { /* 实现 — 含子设备数和点位数字段统计 */ }

    // POST /api/collection-tasks
    [HttpPost]
    public async Task<ApiResult<CollectionTask>> CreateTask([FromBody] CollectionTaskCreateDto dto)
    {
        var task = new CollectionTask
        {
            Name = dto.Name,
            GatewayDeviceId = dto.GatewayDeviceId,
            ConnectionJson = JsonSerializer.Serialize(dto.ConnectionConfig),
            Enabled = dto.Enabled ?? true
        };
        _dbContext.CollectionTasks.Add(task);
        await _dbContext.SaveChangesAsync();
        // 新任务加入调度
        _schedulerManager.AddOrUpdateScheduler(task);
        return new ApiResult<CollectionTask>(ApiCode.Success, task);
    }

    // PUT /api/collection-tasks/{id}
    [HttpPut("{id}")]
    public async Task<ApiResult<CollectionTask>> UpdateTask(Guid id, [FromBody] CollectionTaskUpdateDto dto)
    { /* 实现 — 更新后调用 RefreshSchedulers */ }

    // DELETE /api/collection-tasks/{id}
    [HttpDelete("{id}")]
    public async Task<ApiResult<bool>> DeleteTask(Guid id)
    { /* 实现 — 级联删除关联的 CollectionDevice/CollectionPoint/CollectionLog */ }

    // POST /api/collection-tasks/{id}/start
    [HttpPost("{id}/start")]
    public async Task<ApiResult<bool>> StartTask(Guid id)
    { /* 实现 — 更新 Enabled=true, 调用 AddOrUpdateScheduler */ }

    // POST /api/collection-tasks/{id}/stop
    [HttpPost("{id}/stop")]
    public async Task<ApiResult<bool>> StopTask(Guid id)
    { /* 实现 — 更新 Enabled=false, 调用 RemoveScheduler */ }
}
```

### 3.2 CollectionDeviceController

```csharp
// 新建文件: IoTSharp/Controllers/CollectionDeviceController.cs
[ApiController]
[Route("api/collection-tasks/{taskId}/[controller]")]
[Authorize]
public class CollectionDeviceController : ControllerBase
{
    // GET — 采集设备列表
    [HttpGet]
    public async Task<ApiResult<List<CollectionDeviceDto>>> GetDevices(Guid taskId)
    { /* 实现 */ }

    // POST — 创建采集设备
    [HttpPost]
    public async Task<ApiResult<CollectionDevice>> CreateDevice(Guid taskId, [FromBody] CollectionDeviceCreateDto dto)
    {
        var device = new CollectionDevice
        {
            CollectionTaskId = taskId,
            DeviceId = dto.DeviceId,        // 平台 Device ID
            SlaveId = dto.SlaveId,
            Name = dto.Name
        };
        _dbContext.CollectionDevices.Add(device);
        await _dbContext.SaveChangesAsync();
        return new ApiResult<CollectionDevice>(ApiCode.Success, device);
    }

    // PUT /{id} — 更新采集设备
    [HttpPut("{id}")]
    public async Task<ApiResult<CollectionDevice>> UpdateDevice(Guid taskId, Guid id, [FromBody] CollectionDeviceUpdateDto dto)
    { /* 实现 */ }

    // DELETE /{id}
    [HttpDelete("{id}")]
    public async Task<ApiResult<bool>> DeleteDevice(Guid taskId, Guid id)
    { /* 实现 — 级联删除关联点位 */ }
}
```

### 3.3 CollectionPointController

```csharp
// 新建文件: IoTSharp/Controllers/CollectionPointController.cs
[ApiController]
[Route("api/collection-devices/{deviceId}/[controller]")]
[Authorize]
public class CollectionPointController : ControllerBase
{
    // GET — 采集点位列表
    [HttpGet]
    public async Task<ApiResult<List<CollectionPointDto>>> GetPoints(Guid deviceId)
    { /* 实现 */ }

    // POST — 创建采集点位
    [HttpPost]
    public async Task<ApiResult<CollectionPoint>> CreatePoint(Guid deviceId, [FromBody] CollectionPointCreateDto dto)
    {
        var point = new CollectionPoint
        {
            CollectionDeviceId = deviceId,
            SlaveId = dto.SlaveId,
            FunctionCode = dto.FunctionCode,
            Address = dto.Address,
            Quantity = dto.Quantity ?? 1,
            RawDataType = dto.RawDataType,
            ByteOrder = dto.ByteOrder,
            ReadPeriodMs = dto.ReadPeriodMs,
            TransformsJson = dto.Transforms != null ? JsonSerializer.Serialize(dto.Transforms) : null,
            TargetDeviceId = dto.TargetDeviceId,    // 数据归属设备（可不同于采集设备）
            TargetName = dto.TargetName,
            TargetType = dto.TargetType ?? "Telemetry" // "Telemetry" 或 "Attribute"
        };
        _dbContext.CollectionPoints.Add(point);
        await _dbContext.SaveChangesAsync();
        return new ApiResult<CollectionPoint>(ApiCode.Success, point);
    }

    // PUT /{id} — 更新点位
    [HttpPut("{id}")]
    public async Task<ApiResult<CollectionPoint>> UpdatePoint(Guid deviceId, Guid id, [FromBody] CollectionPointUpdateDto dto)
    { /* 实现 */ }

    // DELETE /{id}
    [HttpDelete("{id}")]
    public async Task<ApiResult<bool>> DeletePoint(Guid deviceId, Guid id)
    { /* 实现 */ }
}
```

## 4. BatchMerger.Optimize() 激活

### 4.1 修改位置

`GatewayScheduler.cs` 的 `ProcessQueueAsync` 方法（约 176-224 行）。

### 4.2 改动

```csharp
// 原来：
var batches = BatchMerger.Merge(tempList);
foreach (var batch in batches)
{
    await OnBatchReadyAsync(batch);
}

// 改为：
var batches = BatchMerger.Merge(tempList);
var optimizedBatches = BatchMerger.Optimize(batches); // 新增这行
foreach (var batch in optimizedBatches)
{
    await OnBatchReadyAsync(batch);
}
```

### 4.3 效果

将多个相邻的寄存器读取合并为一次请求（最多 125 个连续寄存器），减少 MQTT 往返次数。例如，地址 40001-40005 和 40006-40010 两个区间可以合并为 40001-40010 一次读取。

## 5. 超时即时重试

### 5.1 修改位置

`ModbusCollectionService.cs` 的 `SendModbusRequestAsync` 方法中超时回调（约 348-362 行）。

### 5.2 改动

```csharp
// 在 SendModbusRequestAsync 中，将原来的 fire-and-forget 超时回调改为：
_ = Task.Run(async () =>
{
    int retryCount = 0;
    int maxRetries = 1; // 第一阶段再做 1 次即时重试（共 2 次尝试）
    
    while (retryCount <= maxRetries)
    {
        await Task.Delay(timeoutMs);
        
        if (!_pendingRequests.TryGetValue(gatewayName, out var current) ||
            !string.Equals(current.RequestId, requestId, StringComparison.Ordinal))
        {
            // 请求已经收到响应或已被移除，停止重试
            return;
        }
        
        if (retryCount < maxRetries)
        {
            _logger.LogWarning("Modbus retry {Retry}/{Max} for Gateway={Gateway}, RequestId={RequestId}",
                retryCount + 1, maxRetries, gatewayName, requestId);
            // 重发请求（不重建 PendingRequest）
            try
            {
                await _mqttTransport.PublishRequestAsync(gatewayName, requestId, hexPayload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Modbus retry publish failed: Gateway={Gateway}", gatewayName);
            }
        }
        else
        {
            // 最后一次重试也超时
            if (_pendingRequests.TryRemove(gatewayName, out var p))
            {
                _logger.LogWarning("Modbus request timeout after {Retries} retries: Gateway={Gateway}, RequestId={RequestId}",
                    maxRetries, gatewayName, requestId);
                await SaveTimeoutLogAsync(p);
            }
        }
        retryCount++;
    }
});
```

## 6. 采集日志查询 API

```csharp
// 新建: IoTSharp/Controllers/CollectionLogController.cs
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CollectionLogController : ControllerBase
{
    // GET /api/collection-logs?deviceId=&pointId=&taskId=&status=&begin=&end=&page=1&pageSize=20
    [HttpGet]
    public async Task<ApiResult<PagedData<CollectionLogDto>>> GetLogs(
        [FromQuery] Guid? deviceId,      // CollectionDevice ID
        [FromQuery] Guid? pointId,        // CollectionPoint ID
        [FromQuery] Guid? taskId,         // CollectionTask ID
        [FromQuery] string? status,       // "Success" / "Timeout" / "CrcError" / "Error"
        [FromQuery] DateTime? begin,
        [FromQuery] DateTime? end,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    { /* 实现 */ }
}
```

## 7. DTO 定义（新增）

```csharp
// IoTSharp/Dtos/CollectionTaskDtos.cs 或 IoTSharp.Contracts/CollectionTaskDtos.cs
public class CollectionTaskCreateDto
{
    public required string Name { get; set; }
    public Guid GatewayDeviceId { get; set; }
    public CollectionConnectionDto? ConnectionConfig { get; set; }
    public bool? Enabled { get; set; }
}

public class CollectionTaskDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public Guid GatewayDeviceId { get; set; }
    public string GatewayName { get; set; }
    public bool Enabled { get; set; }
    public int DeviceCount { get; set; }
    public int PointCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CollectionDeviceCreateDto
{
    public Guid DeviceId { get; set; }
    public byte SlaveId { get; set; }
    public string? Name { get; set; }
}

public class CollectionPointCreateDto
{
    public byte SlaveId { get; set; }
    public byte FunctionCode { get; set; }
    public ushort Address { get; set; }
    public ushort? Quantity { get; set; }
    public string RawDataType { get; set; }
    public string? ByteOrder { get; set; }
    public int ReadPeriodMs { get; set; }
    public List<TransformDto>? Transforms { get; set; }
    public Guid? TargetDeviceId { get; set; }
    public string? TargetName { get; set; }
    public string? TargetType { get; set; }
}

public class TransformDto
{
    public string Type { get; set; }  // "scale" / "offset" / "clamp" / "bitExtract"
    public double? Factor { get; set; }
    public double? Offset { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public int? BitStart { get; set; }
    public int? BitLength { get; set; }
}
```

## 8. 实施步骤

1. 创建 `CollectionTaskController`（CRUD + 启停）。
2. 创建 `CollectionDeviceController`（CRUD）。
3. 创建 `CollectionPointController`（CRUD）。
4. 激活 `BatchMerger.Optimize()` — 修改 `GatewayScheduler.ProcessQueueAsync`。
5. 实现超时即时重试 — 修改 `ModbusCollectionService.SendModbusRequestAsync`。
6. 创建 `CollectionLogController`（日志查询）。
7. 创建对应的 DTO 文件。
8. 实现前端采集配置管理页面（CollectionTaskList + CollectionDeviceList + CollectionPointForm）。
9. 端到端测试：创建采集任务 → 添加设备和点位 → 启动采集 → 验证数据入库 → 查看采集日志。

## 9. 不需要改动

- `ModbusRtuProtocol` / `ModbusDataParser` — 核心解析逻辑已验证，有单元测试覆盖。
- `GatewaySchedulerManager` / `GatewayScheduler` — 调度逻辑满足需求。
- `ModbusMqttTransport` — MQTT 传输层功能完整。
- `BatchMerger.Merge` — 合并逻辑正确，仅在调用处增加 `Optimize()`。
