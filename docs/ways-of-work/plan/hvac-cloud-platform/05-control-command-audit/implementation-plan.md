# Implementation Plan：控制命令与审计模块

> 对应 PRD：[控制命令与审计模块 PRD](../05-control-command-audit/prd.md)
> 策略：第一阶段最简版 RPC 下发
> 状态：**RPC 通道已有，待补齐命令记录、权限校验和查询 API**

## 1. 实施概述

第一阶段不实现完整状态机，核心工作是：
1. 审查现有 `RpcClient` / `RpcController` 通道完整性。
2. 创建控制命令 HTTP API。
3. 补齐操作审计日志（命令表 + AuditLog 关联）。
4. 补充基本的权限校验。

## 2. 涉及文件

| 优先级 | 文件 | 动作 |
|--------|------|------|
| P0 | `IoTSharp/Controllers/CommandController.cs` | **新建** — 命令下发和查询 API |
| P0 | `IoTSharp.Data/CommandLog.cs` | **新建** — 控制命令日志实体 |
| P1 | `IoTSharp/Extensions/RpcClient.cs` | **审查** — 确认超时和响应处理 |
| P1 | `IoTSharp/Services/MQTTControllers/RpcController.cs` | **审查** — RPC 响应路由 |
| P1 | `IoTSharp.Data/AuditLog.cs` | **审查** — 确认是否满足命令审计需求 |
| P2 | `IoTSharp/Services/CommandService.cs` | **新建** — 命令业务逻辑服务 |
| P3 | `ClientApp/` | 命令操作和日志查询页面 |

## 3. 命令日志实体

```csharp
// 新建: IoTSharp.Data/CommandLog.cs
[Table("CommandLog")]
public class CommandLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    // 设备信息
    public Guid DeviceId { get; set; }
    public Device Device { get; set; }
    
    // 操作人信息
    public Guid OperatorId { get; set; }
    public string OperatorName { get; set; }
    
    // 命令信息
    public string CommandType { get; set; }     // "Start" / "Stop" / "Reset" / "SetValue" / "WriteCoil" / "WriteRegister"
    public string Method { get; set; }           // RPC method name
    public string Parameters { get; set; }       // JSON string
    
    // 状态
    public CommandStatus Status { get; set; }    // Sent / Success / Failed / Timeout
    public string? ErrorMessage { get; set; }
    public string? ResultData { get; set; }      // JSON string — 设备返回的结果
    
    // 时间
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
    public int DurationMs { get; set; }          // 从发送到响应的耗时
}

public enum CommandStatus
{
    Sent = 0,
    Success = 1,
    Failed = 2,
    Timeout = 3
}
```

## 4. CommandService

```csharp
// 新建: IoTSharp/Services/CommandService.cs
public class CommandService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly RpcClient _rpcClient;
    private readonly ILogger<CommandService> _logger;

    /// <summary>
    /// 通过 MQTT RPC 向设备下发控制命令
    /// </summary>
    public async Task<CommandResult> SendCommandAsync(Guid deviceId, Guid operatorId, CommandRequest request)
    {
        // 1. 查找设备和网关
        var device = await _dbContext.Device
            .Include(d => d.DeviceIdentity)
            .FirstOrDefaultAsync(d => d.Id == deviceId);
        if (device == null)
            return CommandResult.Fail("Device not found");

        // 2. 权限校验
        // 验证 operatorId 是否有权限操作此设备（租户/客户范围 + 角色权限）
        var hasPermission = await CheckPermission(operatorId, deviceId);
        if (!hasPermission)
            return CommandResult.Fail("Permission denied");

        // 3. 创建命令日志
        var cmdLog = new CommandLog
        {
            DeviceId = deviceId,
            OperatorId = operatorId,
            OperatorName = "", // 从 UserManager 获取
            CommandType = request.CommandType,
            Method = request.Method,
            Parameters = JsonSerializer.Serialize(request.Params),
            Status = CommandStatus.Sent,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.CommandLogs.Add(cmdLog);
        await _dbContext.SaveChangesAsync();

        // 4. 通过 RPC 下发
        try
        {
            var deviceName = device.Name;
            var rpcPayload = JsonSerializer.Serialize(request);
            var response = await _rpcClient.SendAsync(
                deviceName,
                rpcPayload,
                TimeSpan.FromMilliseconds(request.TimeoutMs));

            // 5. 更新日志
            cmdLog.Status = response.Success ? CommandStatus.Success : CommandStatus.Failed;
            cmdLog.ResultData = response.Data;
            cmdLog.RespondedAt = DateTime.UtcNow;
            cmdLog.DurationMs = (int)(cmdLog.RespondedAt.Value - cmdLog.CreatedAt).TotalMilliseconds;
        }
        catch (TimeoutException)
        {
            cmdLog.Status = CommandStatus.Timeout;
            cmdLog.ErrorMessage = "RPC timeout";
        }
        catch (Exception ex)
        {
            cmdLog.Status = CommandStatus.Failed;
            cmdLog.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Command execution failed: DeviceId={DeviceId}, Command={CommandType}", 
                deviceId, request.CommandType);
        }
        
        await _dbContext.SaveChangesAsync();
        return CommandResult.FromLog(cmdLog);
    }

    private async Task<bool> CheckPermission(Guid operatorId, Guid deviceId)
    {
        // 第一阶段简化：检查操作人和设备是否属于同一租户/客户
        var user = await _dbContext.Users.FindAsync(operatorId.ToString());
        var device = await _dbContext.Device.FindAsync(deviceId);
        // return user.TenantId == device.TenantId;
        return true; // TODO: 实现实际权限校验
    }
}

public class CommandRequest
{
    public string CommandType { get; set; }
    public string Method { get; set; }
    public Dictionary<string, object> Params { get; set; }
    public int TimeoutMs { get; set; } = 5000;
}

public class CommandResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public CommandLog Log { get; set; }

    public static CommandResult Fail(string error) => new() { Success = false, Error = error };
    public static CommandResult FromLog(CommandLog log) => new() 
    { 
        Success = log.Status == CommandStatus.Success, 
        Error = log.ErrorMessage, 
        Log = log 
    };
}
```

## 5. CommandController

```csharp
// 新建: IoTSharp/Controllers/CommandController.cs
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CommandController : ControllerBase
{
    private readonly CommandService _commandService;
    private readonly ApplicationDbContext _dbContext;

    // POST /api/devices/{deviceId}/commands — 下发控制命令
    [HttpPost("/api/devices/{deviceId}/commands")]
    public async Task<ApiResult<CommandLogDto>> SendCommand(Guid deviceId, [FromBody] CommandRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return new ApiResult<CommandLogDto>(ApiCode.Unauthorized, "Not authenticated");
        
        var result = await _commandService.SendCommandAsync(deviceId, Guid.Parse(userId), request);
        if (!result.Success)
            return new ApiResult<CommandLogDto>(ApiCode.Error, result.Error);
        
        return new ApiResult<CommandLogDto>(ApiCode.Success, MapToDto(result.Log));
    }

    // GET /api/devices/{deviceId}/commands — 设备命令历史
    [HttpGet("/api/devices/{deviceId}/commands")]
    public async Task<ApiResult<PagedData<CommandLogDto>>> GetDeviceCommands(
        Guid deviceId,
        [FromQuery] string? commandType,
        [FromQuery] CommandStatus? status,
        [FromQuery] DateTime? begin,
        [FromQuery] DateTime? end,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    { /* 实现 */ }

    // GET /api/commands — 全局命令历史（按权限过滤）
    [HttpGet]
    public async Task<ApiResult<PagedData<CommandLogDto>>> GetCommands(
        [FromQuery] Guid? deviceId,
        [FromQuery] Guid? operatorId,
        [FromQuery] string? commandType,
        [FromQuery] CommandStatus? status,
        [FromQuery] DateTime? begin,
        [FromQuery] DateTime? end,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    { /* 实现 */ }

    // GET /api/commands/{id} — 单条命令详情
    [HttpGet("{id}")]
    public async Task<ApiResult<CommandLogDto>> GetCommand(Guid id)
    { /* 实现 */ }
}
```

## 6. 审查 RpcClient 通道

```csharp
// IoTSharp/Extensions/RpcClient.cs
// 审查要点：
// 1. SendAsync 方法是否支持超时参数
// 2. 响应 Topic 订阅是否正确匹配 RequestId
// 3. 超时后是否正确取消订阅（避免内存泄漏）
// 4. 错误处理：设备不在线时是否抛出明确异常

// 如果当前 RpcClient 缺少超时参数：
public async Task<RpcResponse> SendAsync(string deviceName, string payload, TimeSpan timeout)
{
    var requestId = Guid.NewGuid().ToString("N");
    var tcs = new TaskCompletionSource<RpcResponse>();
    var cts = new CancellationTokenSource(timeout);
    
    // 订阅响应 topic
    var responseTopic = $"devices/{deviceName}/rpc/response/{requestId}";
    // ... MQTT subscribe + 发布请求
    
    cts.Token.Register(() => tcs.TrySetException(new TimeoutException("RPC timeout")));
    return await tcs.Task;
}
```

## 7. 命令类型枚举

```csharp
// 建议在 IoTSharp.Contracts 或 IoTSharp.Data 中定义
public static class CommandTypes
{
    public const string Start = "Start";
    public const string Stop = "Stop";
    public const string Reset = "Reset";
    public const string SetValue = "SetValue";
    public const string WriteCoil = "WriteCoil";
    public const string WriteRegister = "WriteRegister";
    public const string OpenValve = "OpenValve";
    public const string CloseValve = "CloseValve";
    public const string SetFrequency = "SetFrequency";
}
```

## 8. 前端实施要点

```
ClientApp/src/views/commands/
├── CommandPanel.vue          // 设备详情页中的命令操作面板
├── CommandHistory.vue         // 命令历史列表（表格 + 过滤）
└── CommandDetail.vue          // 命令详情（参数 + 响应 + 耗时）
```

## 9. 实施步骤

1. 创建 `CommandLog` 实体 + EF 迁移。
2. 创建 `CommandService`（核心下发逻辑 + 权限校验存根）。
3. 创建 `CommandController`（下发 + 查询 API）。
4. 审查 `RpcClient` — 确认超时和响应处理。
5. 补充权限校验逻辑（至少校验操作人和设备同租户）。
6. 实现前端命令操作面板和命令历史页面。
7. 端到端测试：下发控制命令 → 设备响应 → 日志记录 → 历史查询。

## 10. 不需要改动（第一阶段）

- `RpcController` — MQTT RPC 协议处理逻辑已工作。
- MQTT Broker 配置 — 已支持 RPC Topic 路由。
- 完整状态机 — 推迟到后续阶段。
- 控制模板机制 — 推迟到后续阶段。

## 11. 代码验证审核补充（2026-04-28）

本模块仍是第一阶段最关键缺口之一。当前代码有 RPC 下发能力，但还不能满足 PRD 中“命令生命周期、审计、查询、失败可见性”的要求。

### 11.1 当前真实代码状态

| 能力 | 当前代码 | 审核结论 |
|------|----------|----------|
| HTTP RPC 下发 | `DevicesController.Rpc` | 已存在，但返回 `ActionResult<string>`，不具备命令日志 |
| MQTT RPC 响应 | `IoTSharp/Services/MQTTControllers/RpcController.cs` | 已存在 |
| RPC 客户端 | `IoTSharp/Extensions/RpcClient.cs` | 已存在，需要审查超时、订阅释放和错误语义 |
| 审计基础 | `IoTSharp.Data/AuditLog.cs` | 已存在通用审计实体，但没有控制命令专用状态 |
| 命令日志 | 无 `CommandLog` | 仍需新增 |
| 命令服务/API | 无 `CommandService` / `CommandController` | 仍需新增 |

### 11.2 修正后的实施入口

1. 新增 `CommandLog` 实体、EF 配置和迁移，字段至少包含设备、操作者、方法、参数、状态、错误、响应、耗时、创建时间。
2. 新增 `CommandService`，复用现有 `RpcClient`，不要绕过已有 MQTT RPC 通道。
3. 新增 `CommandController`，路由遵循仓库风格 `api/Command/[action]` 或明确采用 REST 风格并同步前端 API 封装。
4. 下发接口返回 `ApiResult<PagedData<CommandLogDto>>`，新增/更新失败返回空分页对象。
5. 权限校验第一阶段至少校验用户和设备的租户/客户范围；高风险写命令后续再接控制模板。
6. 在设备详情页新增命令面板和命令历史，使用 Element Plus 显式组件。

### 11.3 Agent 验收标准

- 授权用户下发命令后，无论成功、失败或超时，均能查询到 `CommandLog`。
- 设备不存在、无权限、RPC 超时均返回统一结构并记录失败原因。
- 命令历史可按设备、状态、命令类型和时间过滤。
- 不破坏现有 `DevicesController.Rpc` 和 MQTT RPC Topic。
