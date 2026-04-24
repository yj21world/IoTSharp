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
/// 网关调度器 - 管理单个网关的采集任务
/// </summary>
public class GatewayScheduler
{
    private readonly ILogger<GatewayScheduler> _logger;
    private readonly CollectionTask _task;
    private readonly GatewaySchedulerOptions _options;

    // 三个优先级队列 (按 ReadPeriodMs 分组)
    private readonly PriorityQueue<CollectionPoint, int> _highSpeedQueue;     // <15s
    private readonly PriorityQueue<CollectionPoint, int> _mediumSpeedQueue;   // 15-45s
    private readonly PriorityQueue<CollectionPoint, int> _lowSpeedQueue;      // >45s

    // 上次采集时间记录
    private readonly ConcurrentDictionary<Guid, DateTime> _lastCollected = new();

    // 批量合并器
    private readonly BatchMerger _batchMerger = new();

    // 调度状态
    private bool _isRunning;
    private CancellationToken _cancellationToken;

    /// <summary>
    /// 网关设备名称
    /// </summary>
    public string GatewayDeviceName => _task.GatewayDevice?.Name ?? string.Empty;

    /// <summary>
    /// 网关设备ID
    /// </summary>
    public Guid GatewayDeviceId => _task.GatewayDeviceId;

    /// <summary>
    /// 是否正在运行
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    /// <summary>
    /// 最后执行时间
    /// </summary>
    public DateTime LastExecuteAt { get; private set; }

    public GatewayScheduler(
        CollectionTask task,
        GatewaySchedulerOptions options,
        ILogger<GatewayScheduler> logger)
    {
        _task = task ?? throw new ArgumentNullException(nameof(task));
        _options = options ?? new GatewaySchedulerOptions();
        _logger = logger;

        _highSpeedQueue = new PriorityQueue<CollectionPoint, int>();
        _mediumSpeedQueue = new PriorityQueue<CollectionPoint, int>();
        _lowSpeedQueue = new PriorityQueue<CollectionPoint, int>();

        InitializeQueues();
    }

    /// <summary>
    /// 初始化队列
    /// </summary>
    private void InitializeQueues()
    {
        foreach (var device in _task.Devices.Where(d => d.Enabled))
        {
            foreach (var point in device.Points.Where(p => p.Enabled))
            {
                var priority = point.ReadPeriodMs;
                var queue = GetQueueForPeriod(point.ReadPeriodMs);
                queue.Enqueue(point, priority);
            }
        }

        _logger.LogInformation(
            "Initialized scheduler for gateway {Gateway}: High={High}, Medium={Medium}, Low={Low}",
            GatewayDeviceName,
            _highSpeedQueue.Count,
            _mediumSpeedQueue.Count,
            _lowSpeedQueue.Count);
    }

    private PriorityQueue<CollectionPoint, int> GetQueueForPeriod(int periodMs)
    {
        if (periodMs < 15000)
            return _highSpeedQueue;
        else if (periodMs <= 45000)
            return _mediumSpeedQueue;
        else
            return _lowSpeedQueue;
    }

    /// <summary>
    /// 开始运行调度器
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _isRunning = true;
        _cancellationToken = cancellationToken;

        _logger.LogInformation("Gateway scheduler {Gateway} started", GatewayDeviceName);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                LastExecuteAt = DateTime.UtcNow;

                try
                {
                    await ExecuteCycleAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in scheduler cycle for gateway {Gateway}", GatewayDeviceName);
                }

                // 等待一小段时间
                await Task.Delay(100, cancellationToken);
            }
        }
        finally
        {
            _isRunning = false;
            _logger.LogInformation("Gateway scheduler {Gateway} stopped", GatewayDeviceName);
        }
    }

    /// <summary>
    /// 执行一个采集周期
    /// </summary>
    private async Task ExecuteCycleAsync()
    {
        var now = DateTime.UtcNow;

        // 处理高速队列 (10s)
        await ProcessQueueAsync(_highSpeedQueue, 10000, now);

        // 处理中速队列 (30s)
        await ProcessQueueAsync(_mediumSpeedQueue, 30000, now);

        // 处理低速队列 (60s)
        await ProcessQueueAsync(_lowSpeedQueue, 60000, now);
    }

    /// <summary>
    /// 处理队列
    /// </summary>
    private async Task ProcessQueueAsync(
        PriorityQueue<CollectionPoint, int> queue,
        int expectedPeriodMs,
        DateTime now)
    {
        var tempList = new List<CollectionPoint>();

        // 取出所有到期的点位
        while (queue.Count > 0)
        {
            if (!queue.TryPeek(out var point, out var priority))
                break;

            if (_lastCollected.TryGetValue(point.Id, out var lastTime))
            {
                var elapsed = (now - lastTime).TotalMilliseconds;
                if (elapsed < point.ReadPeriodMs)
                {
                    // 未到期，跳出队列处理
                    break;
                }
            }

            queue.TryDequeue(out point, out _);
            tempList.Add(point);
        }

        if (tempList.Count == 0)
            return;

        // 按从站分组并合并批量请求
        var batches = _batchMerger.Merge(tempList);

        _logger.LogDebug(
            "Gateway {Gateway}: Processing {Count} points in {BatchCount} batches",
            GatewayDeviceName, tempList.Count, batches.Count);

        // 触发批量请求事件
        foreach (var batch in batches)
        {
            await OnBatchReadyAsync(batch);
        }
    }

    /// <summary>
    /// 当批次准备好时触发
    /// </summary>
    public event Func<BatchRequest, Task> OnBatchReadyAsync = _ => Task.CompletedTask;

    /// <summary>
    /// 记录点位采集完成
    /// </summary>
    public void RecordCollected(Guid pointId)
    {
        _lastCollected[pointId] = DateTime.UtcNow;
    }

    /// <summary>
    /// 添加或更新点位
    /// </summary>
    public void UpdatePoint(CollectionPoint point)
    {
        // 重新计算优先级并加入对应队列
        var queue = GetQueueForPeriod(point.ReadPeriodMs);
        queue.Enqueue(point, point.ReadPeriodMs);
    }

    /// <summary>
    /// 移除点位
    /// </summary>
    public void RemovePoint(Guid pointId)
    {
        _lastCollected.TryRemove(pointId, out _);
        // 注意：PriorityQueue 不支持直接移除，需要重建队列
        RebuildQueues();
    }

    private void RebuildQueues()
    {
        // 重建所有队列
        _highSpeedQueue.Clear();
        _mediumSpeedQueue.Clear();
        _lowSpeedQueue.Clear();

        InitializeQueues();
    }

    /// <summary>
    /// 获取统计信息
    /// </summary>
    public SchedulerStats GetStats()
    {
        return new SchedulerStats
        {
            GatewayDeviceName = GatewayDeviceName,
            GatewayDeviceId = GatewayDeviceId,
            IsRunning = _isRunning,
            LastExecuteAt = LastExecuteAt,
            HighSpeedCount = _highSpeedQueue.Count,
            MediumSpeedCount = _mediumSpeedQueue.Count,
            LowSpeedCount = _lowSpeedQueue.Count,
            TotalPointsCount = _lastCollected.Count
        };
    }
}

/// <summary>
/// 网关调度器选项
/// </summary>
public class GatewaySchedulerOptions
{
    /// <summary>
    /// 高速队列阈值（毫秒）
    /// </summary>
    public int HighSpeedThresholdMs { get; set; } = 15000;

    /// <summary>
    /// 低速队列阈值（毫秒）
    /// </summary>
    public int LowSpeedThresholdMs { get; set; } = 45000;

    /// <summary>
    /// 默认轮询周期（毫秒）
    /// </summary>
    public int DefaultPeriodMs { get; set; } = 30000;
}

/// <summary>
/// 调度器统计信息
/// </summary>
public class SchedulerStats
{
    public string GatewayDeviceName { get; set; }
    public Guid GatewayDeviceId { get; set; }
    public bool IsRunning { get; set; }
    public DateTime LastExecuteAt { get; set; }
    public int HighSpeedCount { get; set; }
    public int MediumSpeedCount { get; set; }
    public int LowSpeedCount { get; set; }
    public int TotalPointsCount { get; set; }
}