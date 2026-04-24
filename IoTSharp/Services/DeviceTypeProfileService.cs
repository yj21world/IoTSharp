using IoTSharp.Contracts;
using IoTSharp.Data;
using IoTSharp.Data.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace IoTSharp.Services;

/// <summary>
/// 设备类型模板服务
/// </summary>
public class DeviceTypeProfileService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DeviceTypeProfileService> _logger;

    public DeviceTypeProfileService(
        ApplicationDbContext context,
        ILogger<DeviceTypeProfileService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有设备类型模板
    /// </summary>
    public async Task<List<DeviceTypeProfileDto>> GetAllAsync()
    {
        var profiles = await _context.DeviceTypeProfiles
            .Include(p => p.CollectionRules)
            .OrderBy(p => p.DeviceType)
            .ThenBy(p => p.ProfileName)
            .ToListAsync();

        return profiles.Select(MapToDto).ToList();
    }

    /// <summary>
    /// 获取设备类型模板详情
    /// </summary>
    public async Task<DeviceTypeProfileDto> GetByIdAsync(Guid id)
    {
        var profile = await _context.DeviceTypeProfiles
            .Include(p => p.CollectionRules)
            .FirstOrDefaultAsync(p => p.Id == id);

        return profile == null ? null : MapToDto(profile);
    }

    /// <summary>
    /// 创建设备类型模板
    /// </summary>
    public async Task<DeviceTypeProfileDto> CreateAsync(CreateDeviceTypeProfileDto dto)
    {
        // 检查 ProfileKey 是否重复
        if (await _context.DeviceTypeProfiles.AnyAsync(p => p.ProfileKey == dto.ProfileKey))
        {
            throw new InvalidOperationException($"ProfileKey '{dto.ProfileKey}' already exists");
        }

        var profile = new DeviceTypeProfile
        {
            Id = Guid.NewGuid(),
            ProfileKey = dto.ProfileKey,
            ProfileName = dto.ProfileName,
            DeviceType = dto.DeviceType,
            Description = dto.Description,
            Icon = dto.Icon,
            Enabled = dto.Enabled,
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.DeviceTypeProfiles.Add(profile);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created DeviceTypeProfile {ProfileKey}", dto.ProfileKey);

        return MapToDto(profile);
    }

    /// <summary>
    /// 更新设备类型模板
    /// </summary>
    public async Task<DeviceTypeProfileDto> UpdateAsync(Guid id, UpdateDeviceTypeProfileDto dto)
    {
        var profile = await _context.DeviceTypeProfiles
            .Include(p => p.CollectionRules)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (profile == null)
        {
            throw new InvalidOperationException($"DeviceTypeProfile {id} not found");
        }

        profile.ProfileName = dto.ProfileName;
        profile.Description = dto.Description;
        profile.Icon = dto.Icon;
        profile.Enabled = dto.Enabled;
        profile.UpdatedAt = DateTime.UtcNow;
        profile.Version++;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated DeviceTypeProfile {Id}", id);

        return MapToDto(profile);
    }

    /// <summary>
    /// 删除设备类型模板
    /// </summary>
    public async Task DeleteAsync(Guid id)
    {
        var profile = await _context.DeviceTypeProfiles.FindAsync(id);
        if (profile == null)
        {
            throw new InvalidOperationException($"DeviceTypeProfile {id} not found");
        }

        _context.DeviceTypeProfiles.Remove(profile);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted DeviceTypeProfile {Id}", id);
    }

    /// <summary>
    /// 获取模板的采集规则
    /// </summary>
    public async Task<List<CollectionRuleTemplateDto>> GetRulesAsync(Guid profileId)
    {
        var rules = await _context.CollectionRuleTemplates
            .Where(r => r.ProfileId == profileId)
            .OrderBy(r => r.SortOrder)
            .ToListAsync();

        return rules.Select(MapRuleToDto).ToList();
    }

    /// <summary>
    /// 添加采集规则模板
    /// </summary>
    public async Task<CollectionRuleTemplateDto> AddRuleAsync(Guid profileId, CreateCollectionRuleTemplateDto dto)
    {
        var profile = await _context.DeviceTypeProfiles.FindAsync(profileId);
        if (profile == null)
        {
            throw new InvalidOperationException($"DeviceTypeProfile {profileId} not found");
        }

        var rule = new CollectionRuleTemplate
        {
            Id = Guid.NewGuid(),
            ProfileId = profileId,
            PointKey = dto.PointKey,
            PointName = dto.PointName,
            Description = dto.Description,
            FunctionCode = dto.FunctionCode,
            Address = dto.Address,
            RegisterCount = dto.RegisterCount,
            RawDataType = dto.RawDataType,
            ByteOrder = dto.ByteOrder,
            WordOrder = dto.WordOrder,
            ReadPeriodMs = dto.ReadPeriodMs,
            PollingGroup = dto.PollingGroup,
            TransformsJson = dto.TransformsJson,
            TargetName = dto.TargetName,
            TargetType = dto.TargetType,
            TargetValueType = dto.TargetValueType,
            Unit = dto.Unit,
            GroupName = dto.GroupName,
            SortOrder = dto.SortOrder,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.CollectionRuleTemplates.Add(rule);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Added rule {PointKey} to profile {ProfileId}", dto.PointKey, profileId);

        return MapRuleToDto(rule);
    }

    /// <summary>
    /// 更新采集规则模板
    /// </summary>
    public async Task<CollectionRuleTemplateDto> UpdateRuleAsync(Guid ruleId, UpdateCollectionRuleTemplateDto dto)
    {
        var rule = await _context.CollectionRuleTemplates.FindAsync(ruleId);
        if (rule == null)
        {
            throw new InvalidOperationException($"CollectionRuleTemplate {ruleId} not found");
        }

        rule.PointKey = dto.PointKey;
        rule.PointName = dto.PointName;
        rule.Description = dto.Description;
        rule.FunctionCode = dto.FunctionCode;
        rule.Address = dto.Address;
        rule.RegisterCount = dto.RegisterCount;
        rule.RawDataType = dto.RawDataType;
        rule.ByteOrder = dto.ByteOrder;
        rule.WordOrder = dto.WordOrder;
        rule.ReadPeriodMs = dto.ReadPeriodMs;
        rule.PollingGroup = dto.PollingGroup;
        rule.TransformsJson = dto.TransformsJson;
        rule.TargetName = dto.TargetName;
        rule.TargetType = dto.TargetType;
        rule.TargetValueType = dto.TargetValueType;
        rule.Unit = dto.Unit;
        rule.GroupName = dto.GroupName;
        rule.SortOrder = dto.SortOrder;
        rule.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated rule {RuleId}", ruleId);

        return MapRuleToDto(rule);
    }

    /// <summary>
    /// 删除采集规则模板
    /// </summary>
    public async Task DeleteRuleAsync(Guid ruleId)
    {
        var rule = await _context.CollectionRuleTemplates.FindAsync(ruleId);
        if (rule == null)
        {
            throw new InvalidOperationException($"CollectionRuleTemplate {ruleId} not found");
        }

        _context.CollectionRuleTemplates.Remove(rule);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted rule {RuleId}", ruleId);
    }

    /// <summary>
    /// 应用设备类型模板到设备
    /// 将模板的采集规则复制到实际的采集表中
    /// </summary>
    public async Task ApplyProfileToDeviceAsync(Guid deviceId, Guid profileId)
    {
        var profile = await _context.DeviceTypeProfiles
            .Include(p => p.CollectionRules)
            .FirstOrDefaultAsync(p => p.Id == profileId);

        if (profile == null)
        {
            throw new InvalidOperationException($"DeviceTypeProfile {profileId} not found");
        }

        var device = await _context.Device
            .Include(d => d.Owner)
            .Include(d => d.Tenant)
            .Include(d => d.Customer)
            .Include(d => d.DeviceIdentity)
            .FirstOrDefaultAsync(d => d.Id == deviceId);
        if (device == null)
        {
            throw new InvalidOperationException($"Device {deviceId} not found");
        }

        if (device.Owner == null)
        {
            throw new InvalidOperationException($"Device {deviceId} is not bound to a gateway");
        }

        // 绑定设备到模板
        device.HvacDeviceType = profile.DeviceType;
        device.DeviceTypeProfileId = profileId;

        var taskKey = $"profile-{device.Owner.Id:N}";
        var task = await _context.CollectionTasks
            .Include(t => t.Devices)
                .ThenInclude(d => d.Points)
            .FirstOrDefaultAsync(t => t.TaskKey == taskKey);

        if (task == null)
        {
            task = new CollectionTask
            {
                Id = Guid.NewGuid(),
                TaskKey = taskKey,
                GatewayDeviceId = device.Owner.Id,
                Protocol = CollectionProtocolType.Modbus.ToString(),
                Version = 1,
                Enabled = true,
                ConnectionJson = JsonSerializer.Serialize(new CollectionConnectionDto
                {
                    ConnectionKey = taskKey,
                    ConnectionName = $"{device.Owner.Name}-默认连接",
                    Protocol = CollectionProtocolType.Modbus,
                    Transport = "MqttTransparent",
                    TimeoutMs = 3000,
                    RetryCount = 3
                }),
                ReportPolicyJson = JsonSerializer.Serialize(new ReportPolicyDto()),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.CollectionTasks.Add(task);
        }
        else
        {
            task.GatewayDeviceId = device.Owner.Id;
            task.Protocol = CollectionProtocolType.Modbus.ToString();
            task.Enabled = true;
            task.UpdatedAt = DateTime.UtcNow;
            task.Version++;
        }

        var collectionDeviceKey = $"profile-{device.Id:N}";
        var collectionDevice = task.Devices.FirstOrDefault(d => d.DeviceKey == collectionDeviceKey);
        if (collectionDevice == null)
        {
            collectionDevice = new CollectionDevice
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                DeviceKey = collectionDeviceKey,
                CreatedAt = DateTime.UtcNow
            };
            task.Devices.Add(collectionDevice);
        }

        collectionDevice.DeviceName = device.Name;
        collectionDevice.Enabled = true;
        collectionDevice.ProtocolOptionsJson = JsonSerializer.Serialize(new
        {
            SlaveId = ResolveSlaveId(device, profile)
        });
        collectionDevice.SlaveId = ResolveSlaveId(device, profile);
        collectionDevice.UpdatedAt = DateTime.UtcNow;

        var existingPoints = collectionDevice.Points.ToDictionary(p => p.PointKey, StringComparer.OrdinalIgnoreCase);
        var profilePointKeys = profile.CollectionRules.Select(r => r.PointKey).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var obsoletePoint in collectionDevice.Points.Where(p => !profilePointKeys.Contains(p.PointKey)).ToList())
        {
            _context.CollectionPoints.Remove(obsoletePoint);
        }

        foreach (var rule in profile.CollectionRules.OrderBy(r => r.SortOrder).ThenBy(r => r.PointKey))
        {
            if (!existingPoints.TryGetValue(rule.PointKey, out var point))
            {
                point = new CollectionPoint
                {
                    Id = Guid.NewGuid(),
                    DeviceId = collectionDevice.Id,
                    CreatedAt = DateTime.UtcNow
                };
                collectionDevice.Points.Add(point);
            }

            point.PointKey = rule.PointKey;
            point.PointName = string.IsNullOrWhiteSpace(rule.PointName) ? rule.PointKey : rule.PointName;
            point.FunctionCode = rule.FunctionCode;
            point.Address = rule.Address;
            point.RegisterCount = rule.RegisterCount;
            point.RawDataType = rule.RawDataType;
            point.ByteOrder = rule.ByteOrder;
            point.WordOrder = rule.WordOrder;
            point.ReadPeriodMs = rule.ReadPeriodMs;
            point.PollingGroup = rule.PollingGroup;
            point.TransformsJson = rule.TransformsJson;
            point.TargetDeviceId = device.Id;
            point.TargetName = string.IsNullOrWhiteSpace(rule.TargetName) ? rule.PointKey : rule.TargetName;
            point.TargetType = string.IsNullOrWhiteSpace(rule.TargetType) ? CollectionTargetType.Telemetry.ToString() : rule.TargetType;
            point.TargetValueType = string.IsNullOrWhiteSpace(rule.TargetValueType) ? CollectionValueType.Double.ToString() : rule.TargetValueType;
            point.DisplayName = string.IsNullOrWhiteSpace(rule.PointName) ? device.Name : rule.PointName;
            point.Unit = rule.Unit;
            point.GroupName = rule.GroupName;
            point.Enabled = true;
            point.SortOrder = rule.SortOrder;
            point.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        if (device.DeviceIdentity == null)
        {
            _context.AfterCreateDevice(device);
            await _context.SaveChangesAsync();
        }

        _logger.LogInformation("Applied profile {ProfileId} to device {DeviceId} and synchronized collection task {TaskKey}",
            profileId, deviceId, taskKey);
    }

    private static byte ResolveSlaveId(Device device, DeviceTypeProfile profile)
    {
        var seed = Math.Abs(HashCode.Combine(device.Id, profile.Id));
        return (byte)((seed % 247) + 1);
    }

    private static DeviceTypeProfileDto MapToDto(DeviceTypeProfile profile)
    {
        return new DeviceTypeProfileDto
        {
            Id = profile.Id,
            ProfileKey = profile.ProfileKey,
            ProfileName = profile.ProfileName,
            DeviceType = profile.DeviceType,
            Description = profile.Description,
            Icon = profile.Icon,
            Version = profile.Version,
            Enabled = profile.Enabled,
            CreatedAt = profile.CreatedAt,
            UpdatedAt = profile.UpdatedAt,
            Rules = profile.CollectionRules?.Select(MapRuleToDto).ToList() ?? new List<CollectionRuleTemplateDto>()
        };
    }

    private static CollectionRuleTemplateDto MapRuleToDto(CollectionRuleTemplate rule)
    {
        return new CollectionRuleTemplateDto
        {
            Id = rule.Id,
            ProfileId = rule.ProfileId,
            PointKey = rule.PointKey,
            PointName = rule.PointName,
            Description = rule.Description,
            FunctionCode = rule.FunctionCode,
            Address = rule.Address,
            RegisterCount = rule.RegisterCount,
            RawDataType = rule.RawDataType,
            ByteOrder = rule.ByteOrder,
            WordOrder = rule.WordOrder,
            ReadPeriodMs = rule.ReadPeriodMs,
            PollingGroup = rule.PollingGroup,
            TransformsJson = rule.TransformsJson,
            TargetName = rule.TargetName,
            TargetType = rule.TargetType,
            TargetValueType = rule.TargetValueType,
            Unit = rule.Unit,
            GroupName = rule.GroupName,
            SortOrder = rule.SortOrder
        };
    }
}
