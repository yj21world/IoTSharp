using IoTSharp.Contracts;
using IoTSharp.Data;
using IoTSharp.EventBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace IoTSharp.Services.ModbusCollection;

/// <summary>
/// Modbus 采集服务 - 采集引擎入口
/// </summary>
public class ModbusCollectionService : BackgroundService
{
    private const int DefaultRequestTimeoutMs = 5000;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ModbusCollectionService> _logger;
    private readonly IPublisher _publisher;
    private readonly CollectionConfigurationLoader _configLoader;
    private readonly ModbusMqttTransport _mqttTransport;

    // 网关调度器管理器
    private readonly GatewaySchedulerManager _schedulerManager;

    // 请求-响应匹配器
    private readonly ConcurrentDictionary<string, PendingRequest> _pendingRequests = new(StringComparer.OrdinalIgnoreCase);

    // 网关在线状态
    private readonly ConcurrentDictionary<string, bool> _gatewayOnline = new();

    // 配置刷新间隔
    private readonly TimeSpan _configRefreshInterval = TimeSpan.FromSeconds(30);

    public ModbusCollectionService(
        IServiceScopeFactory scopeFactory,
        ILogger<ModbusCollectionService> logger,
        IPublisher publisher,
        GatewaySchedulerManager schedulerManager,
        CollectionConfigurationLoader configLoader,
        ModbusMqttTransport mqttTransport)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _publisher = publisher;
        _configLoader = configLoader;
        _schedulerManager = schedulerManager;
        _mqttTransport = mqttTransport;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ModbusCollectionService starting...");

        try
        {
            // 1. 启动 MQTT transport
            StartMqttTransport();

            // 2. 启动响应消费循环
            _ = ConsumeResponsesAsync(stoppingToken);

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
            StopMqttTransport();
            await StopSchedulersAsync();
            _logger.LogInformation("ModbusCollectionService stopped");
        }
    }

    private void StartMqttTransport()
    {
        _mqttTransport.Start();
    }

    private void StopMqttTransport()
    {
        _mqttTransport.Stop();
    }

    private async Task ConsumeResponsesAsync(CancellationToken stoppingToken)
    {
        await foreach (var response in _mqttTransport.Responses.ReadAllAsync(stoppingToken))
        {
            _logger.LogInformation(
                "Received Modbus response from gateway {Gateway}, payload={Payload}",
                response.GatewayName,
                response.Payload);

            _ = Task.Run(
                () => ProcessModbusResponseAsync(response.GatewayName, response.Payload),
                CancellationToken.None);
        }
    }

    /// <summary>
    /// 处理 Modbus 响应
    /// </summary>
    private async Task ProcessModbusResponseAsync(string gatewayName, string payload)
    {
        try
        {
            // 1. 查找待处理的请求
            if (!_pendingRequests.TryRemove(gatewayName, out var pending))
            {
                _logger.LogWarning("No pending request found for gateway {Gateway}", gatewayName);
                return;
            }

            // 2. 解析响应
            var response = ModbusRtuProtocol.ParseResponse(
                ModbusRtuProtocol.FromHexString(payload),
                pending.FunctionCode);

            response.RequestId = pending.RequestId;
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
            _logger.LogError(ex, "Error processing Modbus response for gateway {Gateway}", gatewayName);
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

            var targetDeviceId = pending.TargetDeviceId ?? pending.GatewayDeviceId;
            var targetName = string.IsNullOrWhiteSpace(pending.TargetName)
                ? pending.PointId.ToString("N")
                : pending.TargetName;

            if (!pending.TargetDeviceId.HasValue)
            {
                _logger.LogInformation(
                    "Use gateway as telemetry target because point target device is not configured: Gateway={Gateway}, Point={Point}, Key={Key}",
                    pending.GatewayDeviceId, pending.PointId, targetName);
            }

            // 3. 写入遥测数据
            if (IsAttributeTarget(pending.TargetType))
            {
                var attributes = new Dictionary<string, object>
                {
                    { targetName, convertedValue }
                };

                await _publisher.PublishAttributeData(new PlayloadData
                {
                    DeviceId = targetDeviceId,
                    MsgBody = attributes,
                    DataSide = DataSide.ClientSide,
                    DataCatalog = DataCatalog.AttributeData
                });

                _logger.LogInformation(
                    "Published attribute: Device={Device}, Key={Key}, Value={Value}",
                    targetDeviceId, targetName, convertedValue);
            }
            else
            {
                var telemetry = new Dictionary<string, object>
                {
                    { targetName, convertedValue }
                };

                await _publisher.PublishTelemetryData(new PlayloadData
                {
                    DeviceId = targetDeviceId,
                    MsgBody = telemetry,
                    DataSide = DataSide.ClientSide,
                    DataCatalog = DataCatalog.TelemetryData
                });

                _logger.LogInformation(
                    "Published telemetry: Device={Device}, Key={Key}, Value={Value}",
                    targetDeviceId, targetName, convertedValue);
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
        string targetType,
        int timeoutMs = DefaultRequestTimeoutMs)
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
            TargetName = targetName,
            TargetType = targetType
        };
        if (!_pendingRequests.TryAdd(gatewayName, pending))
        {
            _logger.LogWarning(
                "Skip Modbus request because gateway already has an in-flight request: Gateway={Gateway}, PointId={PointId}",
                gatewayName,
                pointId);
            return string.Empty;
        }

        // 3. 发布 MQTT 消息
        // Topic: gateway/{gatewayName}/modbus/request/{requestId}
        var topic = ModbusTopic.BuildRequestTopic(gatewayName, requestId);
        try
        {
            await _mqttTransport.PublishRequestAsync(gatewayName, requestId, hexPayload);
        }
        catch
        {
            _pendingRequests.TryRemove(gatewayName, out _);
            throw;
        }

        _logger.LogInformation(
            "Sent Modbus request: Gateway={Gateway}, Topic={Topic}, RequestId={RequestId}, TimeoutMs={TimeoutMs}, Frame={Frame}",
            gatewayName, topic, requestId, timeoutMs, hexPayload);

        // 4. 启动超时检测
        _ = Task.Run(async () =>
        {
            await Task.Delay(timeoutMs);
            if (_pendingRequests.TryGetValue(gatewayName, out var current) &&
                string.Equals(current.RequestId, requestId, StringComparison.Ordinal) &&
                _pendingRequests.TryRemove(gatewayName, out var p))
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
            _logger.LogInformation("Skip Modbus batch for offline gateway {Gateway}", gatewayName);
            return;
        }

        _logger.LogInformation(
            "Sending Modbus batch for gateway {Gateway}: slave={SlaveId}, function={FunctionCode}, points={PointCount}",
            gatewayName,
            batch.SlaveId,
            batch.FunctionCode,
            batch.Points.Count);

        var timeoutMs = GetRequestTimeoutMs(batch.Device?.Task);

        // 遍历批次中的每个点位，发送单独的请求
        foreach (var point in batch.Points)
        {
            if (_pendingRequests.ContainsKey(gatewayName))
            {
                _logger.LogDebug(
                    "Gateway {Gateway} already has an in-flight request, defer remaining points in current batch",
                    gatewayName);
                break;
            }

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
                point.TargetName,
                point.TargetType,
                timeoutMs
            );
        }
    }

    private static bool IsAttributeTarget(string targetType)
    {
        return string.Equals(targetType, CollectionTargetType.Attribute.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private int GetRequestTimeoutMs(CollectionTask task)
    {
        if (string.IsNullOrWhiteSpace(task?.ConnectionJson))
        {
            return DefaultRequestTimeoutMs;
        }

        try
        {
            var connection = JsonSerializer.Deserialize<CollectionConnectionDto>(
                task.ConnectionJson,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            return connection?.TimeoutMs > 0
                ? connection.TimeoutMs
                : DefaultRequestTimeoutMs;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid CollectionTask connection config, use default Modbus timeout {TimeoutMs}ms", DefaultRequestTimeoutMs);
            return DefaultRequestTimeoutMs;
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
    public string TargetType { get; set; }
}
