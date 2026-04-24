using System;

namespace IoTSharp.Services.ModbusCollection;

/// <summary>
/// Modbus RTU 协议栈
/// </summary>
public static class ModbusRtuProtocol
{
    // 功能码常量
    public const byte FUNC_READ_COILS = 0x01;
    public const byte FUNC_READ_DISCRETE = 0x02;
    public const byte FUNC_READ_HOLDING = 0x03;
    public const byte FUNC_READ_INPUT = 0x04;
    public const byte FUNC_WRITE_SINGLE_COIL = 0x05;
    public const byte FUNC_WRITE_SINGLE_REGISTER = 0x06;
    public const byte FUNC_WRITE_MULTIPLE_COILS = 0x0F;
    public const byte FUNC_WRITE_MULTIPLE_REGISTERS = 0x10;

    /// <summary>
    /// 组帧：组装 Modbus RTU 读请求帧
    /// </summary>
    /// <param name="slaveId">从站地址 (1-247)</param>
    /// <param name="functionCode">功能码</param>
    /// <param name="startAddress">起始地址</param>
    /// <param name="quantity">数量</param>
    /// <returns>完整的 Modbus RTU 帧（含 CRC）</returns>
    public static byte[] BuildReadRequest(byte slaveId, byte functionCode, ushort startAddress, ushort quantity)
    {
        var frame = new byte[6];
        frame[0] = slaveId;
        frame[1] = functionCode;
        frame[2] = (byte)(startAddress >> 8);
        frame[3] = (byte)(startAddress & 0xFF);
        frame[4] = (byte)(quantity >> 8);
        frame[5] = (byte)(quantity & 0xFF);

        var crc = CalculateCrc(frame, frame.Length);
        var result = new byte[8];
        Buffer.BlockCopy(frame, 0, result, 0, 6);
        result[6] = (byte)(crc & 0xFF);
        result[7] = (byte)(crc >> 8);

        return result;
    }

    /// <summary>
    /// 组帧：组装 Modbus RTU 写单个寄存器请求帧
    /// </summary>
    public static byte[] BuildWriteSingleRegister(byte slaveId, ushort address, ushort value)
    {
        var frame = new byte[6];
        frame[0] = slaveId;
        frame[1] = FUNC_WRITE_SINGLE_REGISTER;
        frame[2] = (byte)(address >> 8);
        frame[3] = (byte)(address & 0xFF);
        frame[4] = (byte)(value >> 8);
        frame[5] = (byte)(value & 0xFF);

        var crc = CalculateCrc(frame, frame.Length);
        var result = new byte[8];
        Buffer.BlockCopy(frame, 0, result, 0, 6);
        result[6] = (byte)(crc & 0xFF);
        result[7] = (byte)(crc >> 8);

        return result;
    }

    /// <summary>
    /// 解析响应帧
    /// </summary>
    /// <param name="frame">原始响应帧</param>
    /// <param name="expectedFunctionCode">期望的功能码</param>
    /// <returns>解析结果</returns>
    public static ModbusResponse ParseResponse(byte[] frame, byte expectedFunctionCode)
    {
        var response = new ModbusResponse
        {
            RawFrame = frame,
            Status = ResponseStatus.Success
        };

        try
        {
            // 1. 验证帧长度
            if (frame.Length < 5)
            {
                throw new InvalidResponseException("Frame too short");
            }

            // 2. 提取从站地址和功能码
            response.SlaveId = frame[0];
            response.FunctionCode = frame[1];

            // 3. 验证 CRC
            var receivedCrc = (ushort)((frame[frame.Length - 1] << 8) | frame[frame.Length - 2]);
            var calculatedCrc = CalculateCrc(frame, frame.Length - 2);
            if (receivedCrc != calculatedCrc)
            {
                throw new CrcErrorException();
            }

            // 4. 验证功能码 (错误响应会 +0x80)
            if (response.FunctionCode == expectedFunctionCode + 0x80)
            {
                response.Status = ResponseStatus.ModbusError;
                response.ErrorCode = frame[2];
                response.ErrorMessage = $"Modbus error: {(ModbusErrorCode)frame[2]}";
                return response;
            }

            if (response.FunctionCode != expectedFunctionCode)
            {
                throw new InvalidResponseException($"Function code mismatch: expected {expectedFunctionCode}, got {response.FunctionCode}");
            }

            // 5. 提取数据区
            byte byteCount = frame[2];
            response.Data = new byte[byteCount];
            Buffer.BlockCopy(frame, 3, response.Data, 0, byteCount);
        }
        catch (Exception ex) when (ex is not InvalidResponseException && ex is not CrcErrorException)
        {
            response.Status = ResponseStatus.ParseError;
            response.ErrorMessage = ex.Message;
        }

        return response;
    }

    /// <summary>
    /// 计算 CRC16 校验码 (Modbus CRC-16, 多项式: 0xA001)
    /// </summary>
    /// <param name="data">数据</param>
    /// <param name="length">校验长度</param>
    /// <returns>CRC 值</returns>
    public static ushort CalculateCrc(byte[] data, int length)
    {
        ushort crc = 0xFFFF;
        for (int i = 0; i < length; i++)
        {
            crc ^= data[i];
            for (int j = 0; j < 8; j++)
            {
                if ((crc & 0x0001) != 0)
                {
                    crc >>= 1;
                    crc ^= 0xA001;
                }
                else
                {
                    crc >>= 1;
                }
            }
        }
        return crc;
    }

    /// <summary>
    /// 验证 CRC
    /// </summary>
    public static bool VerifyCrc(byte[] frame)
    {
        if (frame.Length < 3)
            return false;

        var receivedCrc = (ushort)((frame[frame.Length - 1] << 8) | frame[frame.Length - 2]);
        var calculatedCrc = CalculateCrc(frame, frame.Length - 2);
        return receivedCrc == calculatedCrc;
    }

    /// <summary>
    /// 将字节数组转换为 Hex 字符串
    /// </summary>
    public static string ToHexString(byte[] data)
    {
        return BitConverter.ToString(data).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// 将 Hex 字符串转换为字节数组
    /// </summary>
    public static byte[] FromHexString(string hex)
    {
        hex = hex.Replace(" ", "").Replace("-", "");
        if (hex.Length % 2 != 0)
            throw new ArgumentException("Hex string must have even length");

        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        return bytes;
    }
}

/// <summary>
/// Modbus 错误码
/// </summary>
public enum ModbusErrorCode : byte
{
    IllegalFunction = 0x01,
    IllegalDataAddress = 0x02,
    IllegalDataValue = 0x03,
    ServerDeviceFailure = 0x04,
    Acknowledge = 0x05,
    ServerDeviceBusy = 0x06,
    NegativeAcknowledge = 0x07,
    MemoryParityError = 0x08,
    GatewayPathUnavailable = 0x0A,
    GatewayTargetDeviceFailedToRespond = 0x0B
}

public class InvalidResponseException : Exception
{
    public InvalidResponseException(string message) : base(message) { }
}

public class CrcErrorException : Exception
{
    public CrcErrorException() : base("CRC verification failed") { }
}
