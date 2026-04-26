using IoTSharp.Contracts;
using IoTSharp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace IoTSharp.Services;

/// <summary>
/// 采集任务服务
/// </summary>
public class CollectionTaskService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CollectionTaskService> _logger;

    public CollectionTaskService(
        ApplicationDbContext context,
        ILogger<CollectionTaskService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有采集任务
    /// </summary>
    public async Task<List<CollectionTask>> GetAllAsync()
    {
        return await _context.CollectionTasks
            .Include(t => t.GatewayDevice)
            .Include(t => t.Devices)
                .ThenInclude(d => d.Points)
            .OrderBy(t => t.TaskKey)
            .ToListAsync();
    }

    /// <summary>
    /// 获取采集任务详情
    /// </summary>
    public async Task<CollectionTask> GetByIdAsync(Guid id)
    {
        return await _context.CollectionTasks
            .Include(t => t.GatewayDevice)
            .Include(t => t.Devices)
                .ThenInclude(d => d.Points)
                    .ThenInclude(p => p.TargetDevice)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    /// <summary>
    /// 获取采集任务详情（通过任务Key）
    /// </summary>
    public async Task<CollectionTask> GetByTaskKeyAsync(string taskKey)
    {
        return await _context.CollectionTasks
            .Include(t => t.GatewayDevice)
            .Include(t => t.Devices)
                .ThenInclude(d => d.Points)
            .FirstOrDefaultAsync(t => t.TaskKey == taskKey);
    }

    /// <summary>
    /// 创建设备类型模板
    /// </summary>
    public async Task<CollectionTask> CreateAsync(CollectionTask entity)
    {
        entity.Id = Guid.NewGuid();
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;

        _context.CollectionTasks.Add(entity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created CollectionTask {TaskKey}", entity.TaskKey);
        return entity;
    }

    /// <summary>
    /// 更新采集任务
    /// </summary>
    public async Task<CollectionTask> UpdateAsync(CollectionTask entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        entity.Version++;

        _context.CollectionTasks.Update(entity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated CollectionTask {Id}", entity.Id);
        return entity;
    }

    /// <summary>
    /// 删除采集任务
    /// </summary>
    public async Task DeleteAsync(Guid id)
    {
        var task = await _context.CollectionTasks.FindAsync(id);
        if (task != null)
        {
            _context.CollectionTasks.Remove(task);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Deleted CollectionTask {Id}", id);
        }
    }

    /// <summary>
    /// 启用/禁用采集任务
    /// </summary>
    public async Task<bool> SetEnabledAsync(Guid id, bool enabled)
    {
        var task = await _context.CollectionTasks.FindAsync(id);
        if (task == null)
            return false;

        task.Enabled = enabled;
        task.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Set CollectionTask {Id} Enabled={Enabled}", id, enabled);
        return true;
    }

    /// <summary>
    /// 获取采集日志
    /// </summary>
    public async Task<(List<CollectionLog> Logs, int Total)> GetLogsAsync(
        Guid? gatewayDeviceId = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        string status = null,
        int offset = 0,
        int limit = 50)
    {
        var query = _context.CollectionLogs.AsQueryable();

        if (gatewayDeviceId.HasValue)
            query = query.Where(l => l.GatewayDeviceId == gatewayDeviceId.Value);

        if (startTime.HasValue)
            query = query.Where(l => l.CreatedAt >= startTime.Value);

        if (endTime.HasValue)
            query = query.Where(l => l.CreatedAt <= endTime.Value);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(l => l.Status == status);

        var total = await query.CountAsync();
        var logs = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();

        return (logs, total);
    }

    /// <summary>
    /// 从 DTO 创建设备类型模板
    /// </summary>
    public async Task<CollectionTask> CreateFromDtoAsync(CollectionTaskDto dto)
    {
        var task = new CollectionTask
        {
            Id = Guid.NewGuid(),
            TaskKey = dto.TaskKey,
            GatewayDeviceId = dto.EdgeNodeId,
            Protocol = dto.Protocol.ToString(),
            Version = dto.Version,
            Enabled = dto.Enabled,
            ConnectionJson = dto.Connection != null ? JsonSerializer.Serialize(dto.Connection) : null,
            ReportPolicyJson = dto.ReportPolicy != null ? JsonSerializer.Serialize(dto.ReportPolicy) : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        foreach (var deviceDto in dto.Devices)
        {
            var device = new CollectionDevice
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                DeviceKey = deviceDto.DeviceKey,
                DeviceName = deviceDto.DeviceName,
                SlaveId = (byte)GetProtocolOptionInt(deviceDto.ProtocolOptions, "SlaveId", 1),
                Enabled = deviceDto.Enabled,
                ProtocolOptionsJson = GetProtocolOptionsJson(deviceDto.ProtocolOptions),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            foreach (var pointDto in deviceDto.Points)
            {
                var point = new CollectionPoint
                {
                    Id = Guid.NewGuid(),
                    DeviceId = device.Id,
                    PointKey = pointDto.PointKey,
                    PointName = pointDto.PointName,
                    FunctionCode = ParseFunctionCode(pointDto.SourceType),
                    Address = ParseAddress(pointDto.Address),
                    RegisterCount = (ushort)pointDto.Length,
                    RawDataType = pointDto.RawValueType,
                    ByteOrder = GetProtocolOptionString(pointDto.ProtocolOptions, "ByteOrder", "AB"),
                    WordOrder = GetProtocolOptionString(pointDto.ProtocolOptions, "WordOrder", "AB"),
                    ReadPeriodMs = pointDto.Polling?.ReadPeriodMs ?? 30000,
                    PollingGroup = pointDto.Polling?.Group,
                    TransformsJson = SerializeTransforms(pointDto.Transforms),
                    TargetName = pointDto.Mapping?.TargetName,
                    TargetType = pointDto.Mapping?.TargetType.ToString(),
                    TargetValueType = pointDto.Mapping?.ValueType.ToString(),
                    DisplayName = pointDto.Mapping?.DisplayName,
                    Unit = pointDto.Mapping?.Unit,
                    GroupName = pointDto.Mapping?.Group,
                    Enabled = pointDto.Enabled,
                    SortOrder = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                device.Points.Add(point);
            }

            task.Devices.Add(device);
        }

        _context.CollectionTasks.Add(task);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created CollectionTask {TaskKey} with {DeviceCount} devices",
            task.TaskKey, task.Devices.Count);

        return task;
    }

    /// <summary>
    /// 使用 DTO 覆盖更新采集任务及其从站、点位明细
    /// </summary>
    public async Task<CollectionTask> UpdateFromDtoAsync(CollectionTask existing, CollectionTaskDto dto)
    {
        existing.TaskKey = dto.TaskKey;
        existing.GatewayDeviceId = dto.EdgeNodeId;
        existing.Protocol = dto.Protocol.ToString();
        existing.Enabled = dto.Enabled;
        existing.ConnectionJson = dto.Connection != null ? JsonSerializer.Serialize(dto.Connection) : null;
        existing.ReportPolicyJson = dto.ReportPolicy != null ? JsonSerializer.Serialize(dto.ReportPolicy) : null;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.Version++;

        var existingDevices = await _context.CollectionDevices
            .Include(d => d.Points)
            .Where(d => d.TaskId == existing.Id)
            .ToListAsync();

        var incomingDevices = dto.Devices?.ToList() ?? new List<CollectionDeviceDto>();
        var incomingKeys = incomingDevices
            .Where(d => !string.IsNullOrWhiteSpace(d.DeviceKey))
            .Select(d => d.DeviceKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var devicesToRemove = existingDevices
            .Where(d => !incomingKeys.Contains(d.DeviceKey))
            .ToList();

        if (devicesToRemove.Count > 0)
        {
            _context.CollectionDevices.RemoveRange(devicesToRemove);
        }

        foreach (var deviceDto in incomingDevices)
        {
            var device = existingDevices.FirstOrDefault(d =>
                string.Equals(d.DeviceKey, deviceDto.DeviceKey, StringComparison.OrdinalIgnoreCase));

            if (device == null)
            {
                device = new CollectionDevice
                {
                    Id = Guid.NewGuid(),
                    TaskId = existing.Id,
                    CreatedAt = DateTime.UtcNow
                };
                _context.CollectionDevices.Add(device);
                existingDevices.Add(device);
            }

            device.DeviceKey = deviceDto.DeviceKey;
            device.DeviceName = deviceDto.DeviceName;
            device.SlaveId = (byte)GetProtocolOptionInt(deviceDto.ProtocolOptions, "SlaveId", 1);
            device.Enabled = deviceDto.Enabled;
            device.ProtocolOptionsJson = GetProtocolOptionsJson(deviceDto.ProtocolOptions);
            device.UpdatedAt = DateTime.UtcNow;

            var existingPoints = device.Points?.ToList() ?? new List<CollectionPoint>();
            var incomingPoints = deviceDto.Points?.ToList() ?? new List<CollectionPointDto>();
            var incomingPointKeys = incomingPoints
                .Where(p => !string.IsNullOrWhiteSpace(p.PointKey))
                .Select(p => p.PointKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var pointsToRemove = existingPoints
                .Where(p => !incomingPointKeys.Contains(p.PointKey))
                .ToList();

            if (pointsToRemove.Count > 0)
            {
                _context.CollectionPoints.RemoveRange(pointsToRemove);
            }

            foreach (var pointDto in incomingPoints)
            {
                var point = existingPoints.FirstOrDefault(p =>
                    string.Equals(p.PointKey, pointDto.PointKey, StringComparison.OrdinalIgnoreCase));

                if (point == null)
                {
                    point = new CollectionPoint
                    {
                        Id = Guid.NewGuid(),
                        DeviceId = device.Id,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.CollectionPoints.Add(point);
                }

                point.PointKey = pointDto.PointKey;
                point.PointName = pointDto.PointName;
                point.FunctionCode = ParseFunctionCode(pointDto.SourceType);
                point.Address = ParseAddress(pointDto.Address);
                point.RegisterCount = (ushort)pointDto.Length;
                point.RawDataType = pointDto.RawValueType;
                point.ByteOrder = GetProtocolOptionString(pointDto.ProtocolOptions, "ByteOrder", "AB");
                point.WordOrder = GetProtocolOptionString(pointDto.ProtocolOptions, "WordOrder", "AB");
                point.ReadPeriodMs = pointDto.Polling?.ReadPeriodMs ?? 30000;
                point.PollingGroup = pointDto.Polling?.Group;
                point.TransformsJson = SerializeTransforms(pointDto.Transforms);
                point.TargetName = pointDto.Mapping?.TargetName;
                point.TargetType = pointDto.Mapping?.TargetType.ToString();
                point.TargetValueType = pointDto.Mapping?.ValueType.ToString();
                point.DisplayName = pointDto.Mapping?.DisplayName;
                point.Unit = pointDto.Mapping?.Unit;
                point.GroupName = pointDto.Mapping?.Group;
                point.Enabled = pointDto.Enabled;
                point.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated CollectionTask {TaskKey} from DTO", existing.TaskKey);

        return await GetByIdAsync(existing.Id);
    }

    /// <summary>
    /// 转换为 DTO
    /// </summary>
    public CollectionTaskDto ToDto(CollectionTask task)
    {
        if (task == null)
            return null;

        var deviceDtos = new List<CollectionDeviceDto>();
        foreach (var device in task.Devices ?? new List<CollectionDevice>())
        {
            var pointDtos = new List<CollectionPointDto>();
            foreach (var point in device.Points ?? new List<CollectionPoint>())
            {
                var pointDto = new CollectionPointDto
                {
                    PointKey = point.PointKey,
                    PointName = point.PointName,
                    SourceType = GetSourceType(point.FunctionCode),
                    Address = point.Address.ToString(),
                    RawValueType = point.RawDataType,
                    Length = point.RegisterCount,
                    Polling = new PollingPolicyDto
                    {
                        ReadPeriodMs = point.ReadPeriodMs,
                        Group = point.PollingGroup
                    },
                    Transforms = !string.IsNullOrEmpty(point.TransformsJson)
                        ? JsonSerializer.Deserialize<List<ValueTransformDto>>(point.TransformsJson)
                        : new List<ValueTransformDto>(),
                    Mapping = new PlatformMappingDto
                    {
                        TargetType = Enum.TryParse<CollectionTargetType>(point.TargetType, out var t) ? t : CollectionTargetType.Telemetry,
                        TargetName = point.TargetName,
                        ValueType = Enum.TryParse<CollectionValueType>(point.TargetValueType, out var v) ? v : CollectionValueType.Double,
                        DisplayName = point.DisplayName,
                        Unit = point.Unit,
                        Group = point.GroupName
                    },
                    ProtocolOptions = JsonSerializer.SerializeToElement(new Dictionary<string, string>
                    {
                        ["ByteOrder"] = string.IsNullOrWhiteSpace(point.ByteOrder) ? "AB" : point.ByteOrder,
                        ["WordOrder"] = string.IsNullOrWhiteSpace(point.WordOrder) ? "AB" : point.WordOrder
                    }),
                    Enabled = point.Enabled
                };
                pointDtos.Add(pointDto);
            }

            var deviceDto = new CollectionDeviceDto
            {
                DeviceKey = device.DeviceKey,
                DeviceName = device.DeviceName,
                Enabled = device.Enabled,
                ProtocolOptions = BuildDeviceProtocolOptions(device),
                Points = pointDtos
            };
            deviceDtos.Add(deviceDto);
        }

        return new CollectionTaskDto
        {
            Id = task.Id,
            TaskKey = task.TaskKey,
            Protocol = Enum.TryParse<CollectionProtocolType>(task.Protocol, out var p) ? p : CollectionProtocolType.Unknown,
            Version = task.Version,
            Enabled = task.Enabled,
            EdgeNodeId = task.GatewayDeviceId,
            Connection = !string.IsNullOrEmpty(task.ConnectionJson)
                ? JsonSerializer.Deserialize<CollectionConnectionDto>(task.ConnectionJson)
                : null,
            ReportPolicy = !string.IsNullOrEmpty(task.ReportPolicyJson)
                ? JsonSerializer.Deserialize<ReportPolicyDto>(task.ReportPolicyJson)
                : null,
            Devices = deviceDtos
        };
    }

    private static byte ParseFunctionCode(string sourceType)
    {
        return sourceType?.ToLowerInvariant() switch
        {
            "coil" => 1,
            "discreteinput" => 2,
            "holdingregister" => 3,
            "inputregister" => 4,
            _ => 3
        };
    }

    private static string GetSourceType(byte functionCode)
    {
        return functionCode switch
        {
            1 => "Coil",
            2 => "DiscreteInput",
            3 => "HoldingRegister",
            4 => "InputRegister",
            _ => "HoldingRegister"
        };
    }

    private static ushort ParseAddress(string address)
    {
        if (ushort.TryParse(address, out var addr))
            return addr;
        return 0;
    }

    private static int GetProtocolOptionInt(JsonElement? protocolOptions, string propertyName, int fallback)
    {
        if (!TryGetProtocolOption(protocolOptions, propertyName, out var value))
        {
            return fallback;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(value.GetString(), out var number) => number,
            _ => fallback
        };
    }

    private static string GetProtocolOptionString(JsonElement? protocolOptions, string propertyName, string fallback)
    {
        if (!TryGetProtocolOption(protocolOptions, propertyName, out var value))
        {
            return fallback;
        }

        return value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()
            : fallback;
    }

    private static string GetProtocolOptionsJson(JsonElement? protocolOptions)
    {
        return IsProtocolOptionsObject(protocolOptions)
            ? protocolOptions.Value.GetRawText()
            : null;
    }

    private static JsonElement BuildDeviceProtocolOptions(CollectionDevice device)
    {
        var options = new Dictionary<string, object>
        {
            ["SlaveId"] = device.SlaveId
        };

        if (!string.IsNullOrWhiteSpace(device.ProtocolOptionsJson))
        {
            using var document = JsonDocument.Parse(device.ProtocolOptionsJson);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                options[property.Name] = property.Value.Deserialize<object>();
            }
        }

        options["SlaveId"] = device.SlaveId;
        return JsonSerializer.SerializeToElement(options);
    }

    private static bool TryGetProtocolOption(JsonElement? protocolOptions, string propertyName, out JsonElement value)
    {
        value = default;
        return IsProtocolOptionsObject(protocolOptions)
            && protocolOptions.Value.TryGetProperty(propertyName, out value);
    }

    private static bool IsProtocolOptionsObject(JsonElement? protocolOptions)
    {
        return protocolOptions.HasValue && protocolOptions.Value.ValueKind == JsonValueKind.Object;
    }

    private static string SerializeTransforms(IReadOnlyList<ValueTransformDto> transforms)
    {
        if (transforms == null || transforms.Count == 0)
            return null;
        return JsonSerializer.Serialize(transforms);
    }
}
