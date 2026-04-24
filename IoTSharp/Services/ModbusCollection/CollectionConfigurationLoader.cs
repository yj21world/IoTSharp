using IoTSharp.Contracts;
using IoTSharp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IoTSharp.Services.ModbusCollection;

/// <summary>
/// 采集配置加载器
/// </summary>
public class CollectionConfigurationLoader
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CollectionConfigurationLoader> _logger;

    public CollectionConfigurationLoader(
        IServiceScopeFactory scopeFactory,
        ILogger<CollectionConfigurationLoader> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// 加载所有启用的采集任务
    /// </summary>
    public async Task<List<CollectionTask>> LoadAllTasksAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var tasks = await context.CollectionTasks
            .Include(t => t.GatewayDevice)
            .Include(t => t.Devices)
                .ThenInclude(d => d.Points)
                    .ThenInclude(p => p.TargetDevice)
            .Where(t => t.Enabled)
            .ToListAsync();

        _logger.LogInformation("Loaded {Count} collection tasks", tasks.Count);
        return tasks;
    }

    /// <summary>
    /// 加载指定网关的采集任务
    /// </summary>
    public async Task<CollectionTask> LoadTaskByGatewayAsync(Guid gatewayDeviceId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var task = await context.CollectionTasks
            .Include(t => t.GatewayDevice)
            .Include(t => t.Devices)
                .ThenInclude(d => d.Points)
                    .ThenInclude(p => p.TargetDevice)
            .FirstOrDefaultAsync(t => t.GatewayDeviceId == gatewayDeviceId && t.Enabled);

        return task;
    }

    /// <summary>
    /// 加载指定任务的完整配置
    /// </summary>
    public async Task<CollectionTask> LoadTaskAsync(Guid taskId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var task = await context.CollectionTasks
            .Include(t => t.GatewayDevice)
            .Include(t => t.Devices)
                .ThenInclude(d => d.Points)
                    .ThenInclude(p => p.TargetDevice)
            .FirstOrDefaultAsync(t => t.Id == taskId);

        return task;
    }

    /// <summary>
    /// 获取所有需要轮询的点位（按网关分组）
    /// </summary>
    public async Task<Dictionary<string, List<CollectionPoint>>> LoadPointsGroupedByGatewayAsync()
    {
        var tasks = await LoadAllTasksAsync();
        var result = new Dictionary<string, List<CollectionPoint>>();

        foreach (var task in tasks)
        {
            if (task.GatewayDevice == null)
            {
                _logger.LogWarning("Task {TaskId} has no gateway device", task.Id);
                continue;
            }

            var gatewayName = task.GatewayDevice.Name;
            var points = task.Devices
                .Where(d => d.Enabled)
                .SelectMany(d => d.Points)
                .Where(p => p.Enabled)
                .ToList();

            if (result.ContainsKey(gatewayName))
            {
                result[gatewayName].AddRange(points);
            }
            else
            {
                result[gatewayName] = points;
            }
        }

        return result;
    }

    /// <summary>
    /// 检查网关是否在线
    /// </summary>
    public async Task<bool> IsGatewayOnlineAsync(Guid gatewayDeviceId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var device = await context.Device.FindAsync(gatewayDeviceId);
        if (device == null)
        {
            return false;
        }

        var active = await context.AttributeLatest
            .Where(a => a.DeviceId == gatewayDeviceId && a.KeyName == Constants._Active)
            .OrderByDescending(a => a.DateTime)
            .Select(a => a.Value_Boolean)
            .FirstOrDefaultAsync();

        if (active != true)
        {
            return false;
        }

        var lastActivity = await context.AttributeLatest
            .Where(a => a.DeviceId == gatewayDeviceId
                && a.DataSide == DataSide.ServerSide
                && a.KeyName == Constants._LastActivityDateTime)
            .OrderByDescending(a => a.DateTime)
            .Select(a => a.Value_DateTime)
            .FirstOrDefaultAsync();

        if (lastActivity == null)
        {
            return false;
        }

        var timeoutSeconds = device.Timeout > 0 ? device.Timeout : 300;
        var isOnline = DateTime.UtcNow.Subtract(lastActivity.Value).TotalSeconds <= timeoutSeconds;

        if (!isOnline)
        {
            _logger.LogDebug(
                "Gateway {GatewayDeviceId} is inactive. LastActivity={LastActivity}, Timeout={TimeoutSeconds}s",
                gatewayDeviceId,
                lastActivity.Value,
                timeoutSeconds);
        }

        return isOnline;
    }
}
