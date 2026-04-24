using System;
using System.Collections.Generic;

namespace IoTSharp.Contracts;

/// <summary>
/// 设备类型模板 DTO
/// </summary>
public record DeviceTypeProfileDto
{
    public Guid Id { get; init; }
    public string ProfileKey { get; init; }
    public string ProfileName { get; init; }
    public HVACDeviceType DeviceType { get; init; }
    public string Description { get; init; }
    public string Icon { get; init; }
    public int Version { get; init; }
    public bool Enabled { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public List<CollectionRuleTemplateDto> Rules { get; init; } = new();
}

/// <summary>
/// 创建设备类型模板 DTO
/// </summary>
public record CreateDeviceTypeProfileDto
{
    public string ProfileKey { get; init; }
    public string ProfileName { get; init; }
    public HVACDeviceType DeviceType { get; init; }
    public string Description { get; init; }
    public string Icon { get; init; }
    public bool Enabled { get; init; } = true;
}

/// <summary>
/// 更新设备类型模板 DTO
/// </summary>
public record UpdateDeviceTypeProfileDto
{
    public string ProfileName { get; init; }
    public string Description { get; init; }
    public string Icon { get; init; }
    public bool Enabled { get; init; }
}

/// <summary>
/// 采集规则模板 DTO
/// </summary>
public record CollectionRuleTemplateDto
{
    public Guid Id { get; init; }
    public Guid ProfileId { get; init; }
    public string PointKey { get; init; }
    public string PointName { get; init; }
    public string Description { get; init; }
    public byte FunctionCode { get; init; }
    public ushort Address { get; init; }
    public ushort RegisterCount { get; init; }
    public string RawDataType { get; init; }
    public string ByteOrder { get; init; }
    public string WordOrder { get; init; }
    public int ReadPeriodMs { get; init; }
    public string PollingGroup { get; init; } = "default";
    public string TransformsJson { get; init; }
    public string TargetName { get; init; }
    public string TargetType { get; init; }
    public string TargetValueType { get; init; }
    public string Unit { get; init; }
    public string GroupName { get; init; }
    public int SortOrder { get; init; }
}

/// <summary>
/// 创建采集规则模板 DTO
/// </summary>
public record CreateCollectionRuleTemplateDto
{
    public string PointKey { get; init; }
    public string PointName { get; init; }
    public string Description { get; init; }
    public byte FunctionCode { get; init; } = 3;
    public ushort Address { get; init; }
    public ushort RegisterCount { get; init; } = 1;
    public string RawDataType { get; init; } = "uint16";
    public string ByteOrder { get; init; } = "AB";
    public string WordOrder { get; init; } = "AB";
    public int ReadPeriodMs { get; init; } = 30000;
    public string PollingGroup { get; init; } = "default";
    public string TransformsJson { get; init; }
    public string TargetName { get; init; }
    public string TargetType { get; init; } = "Telemetry";
    public string TargetValueType { get; init; } = "Double";
    public string Unit { get; init; }
    public string GroupName { get; init; }
    public int SortOrder { get; init; }
}

/// <summary>
/// 更新采集规则模板 DTO
/// </summary>
public record UpdateCollectionRuleTemplateDto
{
    public string PointKey { get; init; }
    public string PointName { get; init; }
    public string Description { get; init; }
    public byte FunctionCode { get; init; }
    public ushort Address { get; init; }
    public ushort RegisterCount { get; init; }
    public string RawDataType { get; init; }
    public string ByteOrder { get; init; }
    public string WordOrder { get; init; }
    public int ReadPeriodMs { get; init; }
    public string PollingGroup { get; init; }
    public string TransformsJson { get; init; }
    public string TargetName { get; init; }
    public string TargetType { get; init; }
    public string TargetValueType { get; init; }
    public string Unit { get; init; }
    public string GroupName { get; init; }
    public int SortOrder { get; init; }
}

/// <summary>
/// 应用设备类型模板 DTO
/// </summary>
public record ApplyDeviceTypeProfileDto
{
    public Guid DeviceId { get; init; }
    public Guid ProfileId { get; init; }
}
