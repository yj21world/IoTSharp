using System;

namespace IoTSharp.Services.ModbusCollection;

/// <summary>
/// Modbus 响应结果
/// </summary>
public class ModbusResponse
{
    /// <summary>
    /// 请求ID
    /// </summary>
    public string RequestId { get; set; }

    /// <summary>
    /// 从站地址
    /// </summary>
    public byte SlaveId { get; set; }

    /// <summary>
    /// 功能码
    /// </summary>
    public byte FunctionCode { get; set; }

    /// <summary>
    /// 原始响应数据
    /// </summary>
    public byte[] Data { get; set; }

    /// <summary>
    /// 原始帧
    /// </summary>
    public byte[] RawFrame { get; set; }

    /// <summary>
    /// 错误码（如果有）
    /// </summary>
    public byte? ErrorCode { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    public ResponseStatus Status { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// 请求时间
    /// </summary>
    public DateTime RequestAt { get; set; }

    /// <summary>
    /// 响应时间
    /// </summary>
    public DateTime ResponseAt { get; set; }
}

public enum ResponseStatus
{
    Success,
    Timeout,
    CrcError,
    NoResponse,
    ParseError,
    ModbusError
}