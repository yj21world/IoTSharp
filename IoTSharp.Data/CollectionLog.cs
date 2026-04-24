using System;

namespace IoTSharp.Data;

/// <summary>
/// 采集日志实体
/// </summary>
public class CollectionLog
{
    /// <summary>
    /// 日志ID
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 网关设备ID
    /// </summary>
    public Guid GatewayDeviceId { get; set; }

    /// <summary>
    /// 任务ID
    /// </summary>
    public Guid? TaskId { get; set; }

    /// <summary>
    /// 从站ID
    /// </summary>
    public Guid? DeviceId { get; set; }

    /// <summary>
    /// 点位ID
    /// </summary>
    public Guid? PointId { get; set; }

    /// <summary>
    /// 请求ID
    /// </summary>
    public string RequestId { get; set; }

    /// <summary>
    /// 请求时间
    /// </summary>
    public DateTime RequestAt { get; set; }

    /// <summary>
    /// 请求帧 Hex 字符串
    /// </summary>
    public string RequestFrame { get; set; }

    /// <summary>
    /// 响应时间（超时为空）
    /// </summary>
    public DateTime? ResponseAt { get; set; }

    /// <summary>
    /// 响应帧 Hex 字符串
    /// </summary>
    public string ResponseFrame { get; set; }

    /// <summary>
    /// 解析后的值
    /// </summary>
    public string ParsedValue { get; set; }

    /// <summary>
    /// 换算后的值
    /// </summary>
    public string ConvertedValue { get; set; }

    /// <summary>
    /// 状态：Success/Timeout/CrcError/NoResponse/ParseError
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// 响应耗时（毫秒）
    /// </summary>
    public int? DurationMs { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}