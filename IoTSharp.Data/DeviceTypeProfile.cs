using IoTSharp.Contracts;
using System;
using System.Collections.Generic;

namespace IoTSharp.Data;

/// <summary>
/// 设备类型模板
/// 为大量同类 HVAC 设备提供全局配置模板
/// </summary>
public class DeviceTypeProfile
{
    /// <summary>
    /// 模板ID
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 模板标识（如 "chiller", "water-pump"）
    /// </summary>
    public string ProfileKey { get; set; }

    /// <summary>
    /// 显示名称（如 "冷水机组"）
    /// </summary>
    public string ProfileName { get; set; }

    /// <summary>
    /// HVAC 设备类型
    /// </summary>
    public HVACDeviceType DeviceType { get; set; }

    /// <summary>
    /// 描述信息
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// 图标
    /// </summary>
    public string Icon { get; set; }

    /// <summary>
    /// 版本号
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 关联的采集规则模板
    /// </summary>
    public ICollection<CollectionRuleTemplate> CollectionRules { get; set; } = new List<CollectionRuleTemplate>();

    /// <summary>
    /// 绑定的设备列表
    /// </summary>
    public ICollection<Device> Devices { get; set; } = new List<Device>();
}