using System;
using System.Collections.Generic;

namespace IoTSharp.Data;

/// <summary>
/// 采集规则模板
/// 预定义的采集点位配置，关联到设备类型模板
/// </summary>
public class CollectionRuleTemplate
{
    /// <summary>
    /// 规则ID
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 所属设备类型模板ID
    /// </summary>
    public Guid ProfileId { get; set; }

    /// <summary>
    /// 点位标识（如 "supply-temp"）
    /// </summary>
    public string PointKey { get; set; }

    /// <summary>
    /// 显示名称（如 "供水温度"）
    /// </summary>
    public string PointName { get; set; }

    /// <summary>
    /// 描述
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Modbus 功能码：1/2/3/4
    /// </summary>
    public byte FunctionCode { get; set; } = 3;

    /// <summary>
    /// 寄存器地址（从0开始）
    /// </summary>
    public ushort Address { get; set; }

    /// <summary>
    /// 寄存器数量
    /// </summary>
    public ushort RegisterCount { get; set; } = 1;

    /// <summary>
    /// 原始数据类型：bool/int16/uint16/int32/uint32/float32/float64/string
    /// </summary>
    public string RawDataType { get; set; } = "uint16";

    /// <summary>
    /// 字节序：AB/CD/ABCD/CDAB/DCBA/BADC
    /// </summary>
    public string ByteOrder { get; set; } = "AB";

    /// <summary>
    /// 字顺序（多寄存器时）
    /// </summary>
    public string WordOrder { get; set; } = "AB";

    /// <summary>
    /// 轮询周期（毫秒）
    /// </summary>
    public int ReadPeriodMs { get; set; } = 30000;

    /// <summary>
    /// 轮询分组
    /// </summary>
    public string PollingGroup { get; set; }

    /// <summary>
    /// 数值转换（JSON）
    /// </summary>
    public string TransformsJson { get; set; }

    /// <summary>
    /// 目标属性名
    /// </summary>
    public string TargetName { get; set; }

    /// <summary>
    /// 目标类型：Telemetry/Attribute/AlarmInput
    /// </summary>
    public string TargetType { get; set; } = "Telemetry";

    /// <summary>
    /// 目标值类型
    /// </summary>
    public string TargetValueType { get; set; } = "Double";

    /// <summary>
    /// 单位
    /// </summary>
    public string Unit { get; set; }

    /// <summary>
    /// 分组名称
    /// </summary>
    public string GroupName { get; set; }

    /// <summary>
    /// 排序
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 导航属性：所属设备类型模板
    /// </summary>
    public DeviceTypeProfile Profile { get; set; }
}