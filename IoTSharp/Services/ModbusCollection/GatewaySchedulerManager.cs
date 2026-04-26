using IoTSharp.Data;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IoTSharp.Services.ModbusCollection;

/// <summary>
/// 网关调度器管理器 - 管理所有网关调度器
/// </summary>
public class GatewaySchedulerManager
{
    private readonly ILogger<GatewaySchedulerManager> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConcurrentDictionary<Guid, GatewayScheduler> _schedulers = new();
    private readonly ConcurrentDictionary<Guid, Task> _runningTasks = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _schedulerTokens = new();
    private readonly ConcurrentDictionary<string, Guid> _gatewayNameToId = new();
    private CancellationTokenSource _cts;
    private Func<BatchRequest, Task> _batchHandler;

    public GatewaySchedulerManager(
        ILogger<GatewaySchedulerManager> logger,
        ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// 添加或更新调度器
    /// </summary>
    public void AddOrUpdateScheduler(CollectionTask task)
    {
        if (task.GatewayDevice == null)
        {
            _logger.LogWarning("Cannot add scheduler for task {TaskId}: no gateway device", task.Id);
            return;
        }

        var gatewayId = task.GatewayDeviceId;
        var gatewayName = task.GatewayDevice.Name;

        if (_schedulers.TryGetValue(gatewayId, out var existingScheduler)
            && existingScheduler.TaskId == task.Id
            && existingScheduler.TaskVersion == task.Version)
        {
            StartSchedulerIfNeeded(gatewayId, existingScheduler);
            _logger.LogDebug(
                "Scheduler for gateway {Gateway}, task {TaskId}, version {Version} is already current",
                gatewayName,
                task.Id,
                task.Version);
            return;
        }

        // 创建新的调度器
        var options = new GatewaySchedulerOptions();
        var scheduler = new GatewayScheduler(
            task,
            options,
            _loggerFactory.CreateLogger<GatewayScheduler>());

        // 添加或替换
        if (_schedulers.TryRemove(gatewayId, out var oldScheduler))
        {
            StopScheduler(gatewayId);
            _logger.LogInformation("Replacing existing scheduler for gateway {Gateway}", gatewayName);
        }

        _schedulers[gatewayId] = scheduler;
        _gatewayNameToId[gatewayName] = gatewayId;

        // 订阅事件
        if (_batchHandler != null)
        {
            scheduler.OnBatchReadyAsync += _batchHandler;
        }

        StartSchedulerIfNeeded(gatewayId, scheduler);

        _logger.LogInformation("Added scheduler for gateway {Gateway}, task {TaskId}", gatewayName, task.Id);
    }

    /// <summary>
    /// 移除调度器
    /// </summary>
    public void RemoveScheduler(Guid gatewayDeviceId)
    {
        if (_schedulers.TryRemove(gatewayDeviceId, out var scheduler))
        {
            StopScheduler(gatewayDeviceId);
            _logger.LogInformation("Removed scheduler for gateway {Gateway}", scheduler.GatewayDeviceName);
        }
    }

    /// <summary>
    /// 获取调度器
    /// </summary>
    public GatewayScheduler GetScheduler(Guid gatewayDeviceId)
    {
        _schedulers.TryGetValue(gatewayDeviceId, out var scheduler);
        return scheduler;
    }

    /// <summary>
    /// 获取调度器
    /// </summary>
    public GatewayScheduler GetSchedulerByName(string gatewayName)
    {
        if (_gatewayNameToId.TryGetValue(gatewayName, out var id))
        {
            return GetScheduler(id);
        }
        return null;
    }

    /// <summary>
    /// 启动所有调度器
    /// </summary>
    public async Task StartAllAsync()
    {
        _cts = new CancellationTokenSource();

        _logger.LogInformation("Starting {Count} gateway schedulers", _schedulers.Count);

        foreach (var item in _schedulers)
        {
            StartSchedulerIfNeeded(item.Key, item.Value);
        }

        _logger.LogInformation("All gateway schedulers started");
        await Task.CompletedTask;
    }

    /// <summary>
    /// 停止所有调度器
    /// </summary>
    public async Task StopAllAsync()
    {
        _cts?.Cancel();
        foreach (var id in _schedulerTokens.Keys.ToList())
        {
            StopScheduler(id);
        }

        _logger.LogInformation("Stopping {Count} gateway schedulers", _schedulers.Count);

        await Task.Delay(500); // 等待调度器自然停止

        _runningTasks.Clear();
        _schedulerTokens.Clear();
        _logger.LogInformation("All gateway schedulers stopped");
    }

    /// <summary>
    /// 刷新调度器
    /// </summary>
    public void Refresh(List<CollectionTask> tasks)
    {
        // 移除不存在的调度器
        var taskIds = tasks.Select(t => t.GatewayDeviceId).ToHashSet();
        foreach (var id in _schedulers.Keys.ToList())
        {
            if (!taskIds.Contains(id))
            {
                RemoveScheduler(id);
            }
        }

        // 添加或更新调度器
        foreach (var task in tasks)
        {
            AddOrUpdateScheduler(task);
        }
    }

    /// <summary>
    /// 获取所有调度器状态
    /// </summary>
    public List<SchedulerStats> GetAllStats()
    {
        return _schedulers.Values.Select(s => s.GetStats()).ToList();
    }

    /// <summary>
    /// 订阅批次就绪事件
    /// </summary>
    public void SubscribeToBatchReady(Func<BatchRequest, Task> handler)
    {
        foreach (var scheduler in _schedulers.Values)
        {
            scheduler.OnBatchReadyAsync += handler;
        }
        _batchHandler = handler;
    }

    private void StartSchedulerIfNeeded(Guid gatewayDeviceId, GatewayScheduler scheduler)
    {
        if (_cts == null || _cts.IsCancellationRequested || scheduler.IsRunning)
        {
            return;
        }

        if (_runningTasks.TryGetValue(gatewayDeviceId, out var runningTask) && !runningTask.IsCompleted)
        {
            return;
        }

        var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        _schedulerTokens[gatewayDeviceId] = linkedTokenSource;
        _runningTasks[gatewayDeviceId] = Task.Run(() => scheduler.RunAsync(linkedTokenSource.Token));
    }

    private void StopScheduler(Guid gatewayDeviceId)
    {
        if (_schedulerTokens.TryRemove(gatewayDeviceId, out var tokenSource))
        {
            tokenSource.Cancel();
            tokenSource.Dispose();
        }

        _runningTasks.TryRemove(gatewayDeviceId, out _);
    }
}
