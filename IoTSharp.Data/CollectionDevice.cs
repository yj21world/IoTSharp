using System;
using System.Collections.Generic;

namespace IoTSharp.Data;

/// <summary>
/// 采集从站实体
/// </summary>
public class CollectionDevice
{
    /// <summary>
    /// 从站ID
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 所属任务ID
    /// </summary>
    public Guid TaskId { get; set; }

    /// <summary>
    /// 从站标识（如 "slave-1"）
    /// </summary>
    public string DeviceKey { get; set; }

    /// <summary>
    /// 从站名称（如 "锅炉控制器1"）
    /// </summary>
    public string DeviceName { get; set; }

    /// <summary>
    /// Modbus 从站地址 (1-247)
    /// </summary>
    public byte SlaveId { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 排序顺序
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 协议特有配置（JSON）：{baudRate, dataBits, stopBits, parity}
    /// </summary>
    public string ProtocolOptionsJson { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 导航属性：所属任务
    /// </summary>
    public CollectionTask Task { get; set; }

    /// <summary>
    /// 导航属性：采集点位列表
    /// </summary>
    public ICollection<CollectionPoint> Points { get; set; } = new List<CollectionPoint>();
}