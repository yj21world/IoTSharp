using System;

namespace IoTSharp.Data;

/// <summary>
/// 采集点位实体
/// </summary>
public class CollectionPoint
{
    /// <summary>
    /// 点位ID
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 所属从站ID
    /// </summary>
    public Guid DeviceId { get; set; }

    /// <summary>
    /// 点位标识（如 "supply-temp"）
    /// </summary>
    public string PointKey { get; set; }

    /// <summary>
    /// 点位名称（如 "供水温度"）
    /// </summary>
    public string PointName { get; set; }

    /// <summary>
    /// Modbus 功能码：1/2/3/4
    /// </summary>
    public byte FunctionCode { get; set; }

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
    /// 轮询分组（用于批量优化）
    /// </summary>
    public string PollingGroup { get; set; }

    /// <summary>
    /// 数值转换（JSON）：[{type:"Scale",params:{factor:0.1}},...]
    /// </summary>
    public string TransformsJson { get; set; }

    /// <summary>
    /// 目标子设备ID
    /// </summary>
    public Guid? TargetDeviceId { get; set; }

    /// <summary>
    /// 子设备属性名（如 "supplyTemperature"）
    /// </summary>
    public string TargetName { get; set; }

    /// <summary>
    /// 目标类型：Telemetry/Attribute/AlarmInput
    /// </summary>
    public string TargetType { get; set; } = "Telemetry";

    /// <summary>
    /// 目标值类型：boolean/int/long/double/string
    /// </summary>
    public string TargetValueType { get; set; } = "Double";

    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// 单位（如 "°C"）
    /// </summary>
    public string Unit { get; set; }

    /// <summary>
    /// 分组名称（如 "boiler"）
    /// </summary>
    public string GroupName { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 排序顺序
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
    /// 导航属性：所属从站
    /// </summary>
    public CollectionDevice Device { get; set; }

    /// <summary>
    /// 导航属性：目标子设备
    /// </summary>
    public Device TargetDevice { get; set; }
}