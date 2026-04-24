using System;
using System.Collections.Generic;

namespace IoTSharp.Data;

/// <summary>
/// 采集任务实体
/// </summary>
public class CollectionTask
{
    /// <summary>
    /// 任务ID
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 任务标识（如 "hvac-boiler-room-a"）
    /// </summary>
    public string TaskKey { get; set; }

    /// <summary>
    /// 关联网关设备ID
    /// </summary>
    public Guid GatewayDeviceId { get; set; }

    /// <summary>
    /// 协议类型（默认 Modbus）
    /// </summary>
    public string Protocol { get; set; } = "Modbus";

    /// <summary>
    /// 配置版本号
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 连接配置（JSON）：{host, port, timeoutMs, retryCount}
    /// </summary>
    public string ConnectionJson { get; set; }

    /// <summary>
    /// 上报策略（JSON）：{defaultTrigger, deadband, includeQuality}
    /// </summary>
    public string ReportPolicyJson { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 导航属性：关联网关
    /// </summary>
    public Gateway GatewayDevice { get; set; }

    /// <summary>
    /// 导航属性：采集从站列表
    /// </summary>
    public ICollection<CollectionDevice> Devices { get; set; } = new List<CollectionDevice>();
}