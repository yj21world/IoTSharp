using IoTSharp.Contracts;
using IoTSharp.Data;
using IoTSharp.EventBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Server;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IoTSharp.Services.ModbusCollection;

/// <summary>
/// Modbus 采集服务 - 采集引擎入口
/// </summary>
public class ModbusCollectionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MqttServer _mqttServer;
    private readonly ILogger<ModbusCollectionService> _logger;
    private readonly IPublisher _publisher;
    private readonly CollectionConfigurationLoader _configLoader;

    // 网关调度器管理器
    private readonly GatewaySchedulerManager _schedulerManager;

    // 请求-响应匹配器
    private readonly ConcurrentDictionary<string, PendingRequest> _pendingRequests = new();

    // 网关在线状态
    private readonly ConcurrentDictionary<string, bool> _gatewayOnline = new();

    // 配置刷新间隔
    private readonly TimeSpan _configRefreshInterval = TimeSpan.FromSeconds(30);

    public ModbusCollectionService(
        IServiceScopeFactory scopeFactory,
        MqttServer mqttServer,
        ILogger<ModbusCollectionService> logger,
        IPublisher publisher,
        GatewaySchedulerManager schedulerManager,
        CollectionConfigurationLoader configLoader)
    {
        _scopeFactory = scopeFactory;
        _mqttServer = mqttServer;
        _logger = logger;
        _publisher = publisher;
        _configLoader = configLoader;
        _schedulerManager = schedulerManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ModbusCollectionService starting...");

        try
        {
            // 1. 订阅 MQTT 消息处理
            await SubscribeToMqttTopicsAsync();

            // 2. 监听 MQTT 客户端事件
            SubscribeToMqttEvents();

            // 3. 加载配置并启动调度器
            await LoadAndStartSchedulersAsync();

            // 4. 启动配置刷新定时器
            _ = ConfigRefreshLoopAsync(stoppingToken);

            _logger.LogInformation("ModbusCollectionService started successfully");

            // 5. 保持服务运行
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("ModbusCollectionService stopping...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ModbusCollectionService encountered an error");
            throw;
        }
        finally
        {
            await StopSchedulersAsync();
            _logger.LogInformation("ModbusCollectionService stopped");
        }
    }

    /// <summary>
    /// 订阅 MQTT Topic
    /// </summary>
    private Task SubscribeToMqttTopicsAsync()
    {
        // 订阅所有网关的响应 Topic
        // Topic: gateway/{gatewayName}/modbus/response/{requestId}
        _logger.LogInformation("Subscribing to MQTT topics: gateway/+/modbus/response/+");
        // 注：实际订阅在 MqttServer 上通过事件处理
        return Task.CompletedTask;
    }

    /// <summary>
    /// 监听 MQTT 事件
    /// </summary>
    private void SubscribeToMqttEvents()
    {
        // 监听客户端发布到 Broker 的消息
        _mqttServer.InterceptingPublishAsync += HandleMqttMessageAsync;
    }

    /// <summary>
    /// 处理 MQTT 消息
    /// </summary>
    private Task HandleMqttMessageAsync(InterceptingPublishEventArgs e)
    {
        var topic = e.ApplicationMessage.Topic;

        // Topic: gateway/{gatewayName}/modbus/response/{requestId}
        if (!topic.StartsWith("gateway/") || !topic.Contains("/modbus/response/"))
        {
            return Task.CompletedTask;
        }

        try
        {
            var parts = topic.Split('/');
            if (parts.Length < 5)
                return Task.CompletedTask;

            var gatewayName = parts[1];
            var requestId = parts[4];
            var payload = e.ApplicationMessage.ConvertPayloadToString() ?? string.Empty;

            _logger.LogDebug(
                "Received Modbus response from gateway {Gateway}, requestId={RequestId}, payload={Payload}",
                gatewayName, requestId, payload);

            // 在后台处理响应
            _ = Task.Run(() => ProcessModbusResponseAsync(gatewayName, requestId, payload));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling MQTT message for topic {Topic}", topic);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 处理 Modbus 响应
    /// </summary>
    private async Task ProcessModbusResponseAsync(string gatewayName, string requestId, string payload)
    {
        try
        {
            // 1. 查找待处理的请求
            if (!_pendingRequests.TryRemove(requestId, out var pending))
            {
                _logger.LogWarning("No pending request found for requestId={RequestId}", requestId);
                return;
            }

            // 2. 解析响应
            var response = ModbusRtuProtocol.ParseResponse(
                ModbusRtuProtocol.FromHexString(payload),
                pending.FunctionCode);

            response.RequestId = requestId;
            response.RequestAt = pending.RequestAt;
            response.ResponseAt = DateTime.UtcNow;

            // 3. 记录日志
            await SaveCollectionLogAsync(pending, response);

            // 4. 如果成功，解析数据并写入遥测
            if (response.Status == ResponseStatus.Success)
            {
                await ProcessSuccessfulResponseAsync(pending, response);
            }
            else
            {
                _logger.LogWarning(
                    "Modbus request failed for gateway {Gateway}, point {Point}: {Status} - {Error}",
                    gatewayName, pending.PointId, response.Status, response.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Modbus response for requestId={RequestId}", requestId);
        }
    }

    /// <summary>
    /// 处理成功的响应
    /// </summary>
    private async Task ProcessSuccessfulResponseAsync(PendingRequest pending, ModbusResponse response)
    {
        try
        {
            // 1. 解析原始值
            var rawValue = ModbusDataParser.ParseRegisters(
                response.Data,
                pending.RawDataType,
                pending.ByteOrder);

            // 2. 应用换算规则
            var convertedValue = ModbusDataParser.ApplyTransforms(
                Convert.ToDouble(rawValue),
                pending.TransformsJson);

            // 3. 写入遥测数据
            if (pending.TargetDeviceId.HasValue)
            {
                var telemetry = new Dictionary<string, object>
                {
                    { pending.TargetName, convertedValue }
                };

                await _publisher.PublishTelemetryData(new PlayloadData
                {
                    DeviceId = pending.TargetDeviceId.Value,
                    MsgBody = telemetry,
                    DataSide = DataSide.ClientSide,
                    DataCatalog = DataCatalog.TelemetryData
                });

                _logger.LogDebug(
                    "Published telemetry: Device={Device}, Key={Key}, Value={Value}",
                    pending.TargetDeviceId, pending.TargetName, convertedValue);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing successful Modbus response");
        }
    }

    /// <summary>
    /// 保存采集日志
    /// </summary>
    private async Task SaveCollectionLogAsync(PendingRequest pending, ModbusResponse response)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var log = new CollectionLog
        {
            GatewayDeviceId = pending.GatewayDeviceId,
            TaskId = pending.TaskId,
            DeviceId = pending.DeviceId,
            PointId = pending.PointId,
            RequestId = pending.RequestId,
            RequestAt = pending.RequestAt,
            RequestFrame = pending.RequestFrame,
            ResponseAt = response.ResponseAt,
            ResponseFrame = ModbusRtuProtocol.ToHexString(response.RawFrame),
            Status = response.Status.ToString(),
            ErrorMessage = response.ErrorMessage,
            DurationMs = (int)(response.ResponseAt - response.RequestAt).TotalMilliseconds
        };

        context.CollectionLogs.Add(log);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// 发送 Modbus 请求
    /// </summary>
    public async Task<string> SendModbusRequestAsync(
        string gatewayName,
        Guid gatewayDeviceId,
        Guid taskId,
        Guid deviceId,
        Guid pointId,
        byte slaveId,
        byte functionCode,
        ushort address,
        ushort quantity,
        string rawDataType,
        string byteOrder,
        string transformsJson,
        Guid? targetDeviceId,
        string targetName,
        int timeoutMs = 2000)
    {
        var requestId = Guid.NewGuid().ToString("N");
        var requestAt = DateTime.UtcNow;

        // 1. 组帧
        var frame = ModbusRtuProtocol.BuildReadRequest(slaveId, functionCode, address, quantity);
        var hexPayload = ModbusRtuProtocol.ToHexString(frame);

        // 2. 保存待处理请求
        var pending = new PendingRequest
        {
            RequestId = requestId,
            GatewayDeviceId = gatewayDeviceId,
            TaskId = taskId,
            DeviceId = deviceId,
            PointId = pointId,
            FunctionCode = functionCode,
            RequestAt = requestAt,
            RequestFrame = hexPayload,
            RawDataType = rawDataType,
            ByteOrder = byteOrder,
            TransformsJson = transformsJson,
            TargetDeviceId = targetDeviceId,
            TargetName = targetName
        };
        _pendingRequests[requestId] = pending;

        // 3. 发布 MQTT 消息
        // Topic: gateway/{gatewayName}/modbus/request/{requestId}
        var topic = $"gateway/{gatewayName}/modbus/request/{requestId}";
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(hexPayload)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce)
            .Build();

        await _mqttServer.PublishAsync(gatewayName, message);

        _logger.LogDebug(
            "Sent Modbus request: Gateway={Gateway}, RequestId={RequestId}, Frame={Frame}",
            gatewayName, requestId, hexPayload);

        // 4. 启动超时检测
        _ = Task.Run(async () =>
        {
            await Task.Delay(timeoutMs);
            if (_pendingRequests.TryRemove(requestId, out var p))
            {
                _logger.LogWarning(
                    "Modbus request timeout: Gateway={Gateway}, RequestId={RequestId}",
                    gatewayName, requestId);

                // 记录超时日志
                await SaveTimeoutLogAsync(p);
            }
        });

        return requestId;
    }

    private async Task SaveTimeoutLogAsync(PendingRequest pending)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var log = new CollectionLog
        {
            GatewayDeviceId = pending.GatewayDeviceId,
            TaskId = pending.TaskId,
            DeviceId = pending.DeviceId,
            PointId = pending.PointId,
            RequestId = pending.RequestId,
            RequestAt = pending.RequestAt,
            RequestFrame = pending.RequestFrame,
            Status = ResponseStatus.Timeout.ToString(),
            ErrorMessage = "Request timeout"
        };

        context.CollectionLogs.Add(log);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// 加载配置并启动调度器
    /// </summary>
    private async Task LoadAndStartSchedulersAsync()
    {
        var tasks = await _configLoader.LoadAllTasksAsync();

        foreach (var task in tasks)
        {
            if (task.GatewayDevice == null)
            {
                _logger.LogWarning("Task {TaskId} has no gateway device", task.Id);
                continue;
            }

            _schedulerManager.AddOrUpdateScheduler(task);
        }

        // 订阅调度器的批次就绪事件
        _schedulerManager.SubscribeToBatchReady(async batch =>
        {
            await SendBatchRequestAsync(batch);
        });

        // 启动所有调度器
        await _schedulerManager.StartAllAsync();

        _logger.LogInformation("Loaded {Count} collection tasks and started schedulers", tasks.Count);
    }

    /// <summary>
    /// 发送批量请求
    /// </summary>
    private async Task SendBatchRequestAsync(BatchRequest batch)
    {
        var gatewayName = batch.Device?.Task?.GatewayDevice?.Name;
        if (string.IsNullOrEmpty(gatewayName))
        {
            _logger.LogWarning("Cannot send batch request: no gateway name");
            return;
        }

        var gatewayDeviceId = batch.Device?.Task?.GatewayDeviceId ?? Guid.Empty;
        var taskId = batch.Device?.TaskId ?? Guid.Empty;

        if (gatewayDeviceId == Guid.Empty)
        {
            _logger.LogWarning("Cannot send batch request for gateway {Gateway}: gateway id is empty", gatewayName);
            return;
        }

        var isGatewayOnline = await _configLoader.IsGatewayOnlineAsync(gatewayDeviceId);
        _gatewayOnline[gatewayName] = isGatewayOnline;

        if (!isGatewayOnline)
        {
            _logger.LogDebug("Skip Modbus batch for offline gateway {Gateway}", gatewayName);
            return;
        }

        // 遍历批次中的每个点位，发送单独的请求
        foreach (var point in batch.Points)
        {
            await SendModbusRequestAsync(
                gatewayName,
                gatewayDeviceId,
                taskId,
                batch.Device.Id,
                point.Id,
                batch.SlaveId,
                batch.FunctionCode,
                point.Address,
                point.RegisterCount,
                point.RawDataType,
                point.ByteOrder,
                point.TransformsJson,
                point.TargetDeviceId,
                point.TargetName
            );
        }
    }

    /// <summary>
    /// 停止所有调度器
    /// </summary>
    private async Task StopSchedulersAsync()
    {
        await _schedulerManager.StopAllAsync();
    }

    /// <summary>
    /// 配置刷新循环
    /// </summary>
    private async Task ConfigRefreshLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_configRefreshInterval, stoppingToken);
                await RefreshSchedulersAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing schedulers");
            }
        }
    }

    /// <summary>
    /// 刷新调度器
    /// </summary>
    private async Task RefreshSchedulersAsync()
    {
        var tasks = await _configLoader.LoadAllTasksAsync();
        _schedulerManager.Refresh(tasks);
        _logger.LogDebug("Refreshed {Count} collection schedulers", tasks.Count);
    }
}

/// <summary>
/// 待处理的请求
/// </summary>
internal class PendingRequest
{
    public string RequestId { get; set; }
    public Guid GatewayDeviceId { get; set; }
    public Guid TaskId { get; set; }
    public Guid DeviceId { get; set; }
    public Guid PointId { get; set; }
    public byte FunctionCode { get; set; }
    public DateTime RequestAt { get; set; }
    public string RequestFrame { get; set; }
    public string RawDataType { get; set; }
    public string ByteOrder { get; set; }
    public string TransformsJson { get; set; }
    public Guid? TargetDeviceId { get; set; }
    public string TargetName { get; set; }
}
