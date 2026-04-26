using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace IoTSharp.Services.ModbusCollection;

/// <summary>
/// Modbus 数据解析器
/// </summary>
public static class ModbusDataParser
{
    /// <summary>
    /// 将字节数组解析为指定类型的数值
    /// </summary>
    public static object ParseRegisters(byte[] data, string dataType, string byteOrder)
    {
        var registers = ToUInt16Array(data);
        var normalizedDataType = dataType?.ToLowerInvariant() ?? string.Empty;

        return normalizedDataType switch
        {
            "bool" => ParseBool(registers),
            "int16" => ParseInt16(registers),
            "uint16" => registers[0],
            "int32" => ParseInt32(registers, NormalizeByteOrder(byteOrder, 4)),
            "uint32" => ParseUInt32(registers, NormalizeByteOrder(byteOrder, 4)),
            "float32" => ParseFloat32(registers, NormalizeByteOrder(byteOrder, 4)),
            "float64" => ParseFloat64(registers, byteOrder),
            "string" => ParseString(registers),
            _ => registers[0]
        };
    }

    /// <summary>
    /// 解析布尔值
    /// </summary>
    public static bool ParseBool(ushort[] registers)
    {
        return (registers[0] & 0x01) == 1;
    }

    /// <summary>
    /// 解析 Int16
    /// </summary>
    public static short ParseInt16(ushort[] registers)
    {
        return (short)registers[0];
    }

    /// <summary>
    /// 解析 UInt16
    /// </summary>
    public static ushort ParseUInt16(ushort[] registers)
    {
        return registers[0];
    }

    /// <summary>
    /// 解析 Int32 (32位有符号整数)
    /// </summary>
    public static int ParseInt32(ushort[] registers, string byteOrder)
    {
        EnsureRegisterCount(registers, 2, nameof(ParseInt32));
        var bytes = ToBytes(registers, byteOrder);
        return BitConverter.ToInt32(bytes, 0);
    }

    /// <summary>
    /// 解析 UInt32 (32位无符号整数)
    /// </summary>
    public static uint ParseUInt32(ushort[] registers, string byteOrder)
    {
        EnsureRegisterCount(registers, 2, nameof(ParseUInt32));
        var bytes = ToBytes(registers, byteOrder);
        return BitConverter.ToUInt32(bytes, 0);
    }

    /// <summary>
    /// 解析 Float32 (32位浮点数)
    /// </summary>
    public static float ParseFloat32(ushort[] registers, string byteOrder)
    {
        EnsureRegisterCount(registers, 2, nameof(ParseFloat32));
        var bytes = ToBytes(registers, byteOrder);
        return BitConverter.ToSingle(bytes, 0);
    }

    /// <summary>
    /// 解析 Float64 (64位浮点数)
    /// </summary>
    public static double ParseFloat64(ushort[] registers, string byteOrder)
    {
        EnsureRegisterCount(registers, 4, nameof(ParseFloat64));
        var bytes = new byte[8];
        var wordBytes = ToBytes(registers, byteOrder);
        Buffer.BlockCopy(wordBytes, 0, bytes, 0, 8);
        return BitConverter.ToDouble(bytes, 0);
    }

    /// <summary>
    /// 解析字符串
    /// </summary>
    public static string ParseString(ushort[] registers)
    {
        var bytes = new byte[registers.Length * 2];
        for (int i = 0; i < registers.Length; i++)
        {
            bytes[i * 2] = (byte)(registers[i] >> 8);
            bytes[i * 2 + 1] = (byte)(registers[i] & 0xFF);
        }
        return System.Text.Encoding.ASCII.GetString(bytes).TrimEnd('\0');
    }

    /// <summary>
    /// 将字节数组转换为 ushort 数组
    /// </summary>
    private static ushort[] ToUInt16Array(byte[] data)
    {
        var registers = new ushort[data.Length / 2];
        for (int i = 0; i < registers.Length; i++)
        {
            registers[i] = (ushort)((data[i * 2] << 8) | data[i * 2 + 1]);
        }
        return registers;
    }

    /// <summary>
    /// 根据字节序将 ushort 数组转换为字节数组
    /// </summary>
    private static byte[] ToBytes(ushort[] registers, string byteOrder)
    {
        return (byteOrder ?? string.Empty).ToUpperInvariant() switch
        {
            "AB" => new[] { (byte)(registers[0] >> 8), (byte)(registers[0] & 0xFF) },
            "BA" => new[] { (byte)(registers[0] & 0xFF), (byte)(registers[0] >> 8) },
            "ABCD" => new[] {
                (byte)(registers[0] >> 8), (byte)(registers[0] & 0xFF),
                (byte)(registers[1] >> 8), (byte)(registers[1] & 0xFF)
            },
            "CDAB" => new[] {
                (byte)(registers[1] >> 8), (byte)(registers[1] & 0xFF),
                (byte)(registers[0] >> 8), (byte)(registers[0] & 0xFF)
            },
            "DCBA" => new[] {
                (byte)(registers[1] & 0xFF), (byte)(registers[1] >> 8),
                (byte)(registers[0] & 0xFF), (byte)(registers[0] >> 8)
            },
            "BADC" => new[] {
                (byte)(registers[0] & 0xFF), (byte)(registers[0] >> 8),
                (byte)(registers[1] & 0xFF), (byte)(registers[1] >> 8)
            },
            _ => throw new ArgumentException($"Unsupported byte order: {byteOrder}")
        };
    }

    private static string NormalizeByteOrder(string byteOrder, int byteCount)
    {
        if (byteCount != 4)
        {
            return byteOrder;
        }

        return (byteOrder ?? string.Empty).ToUpperInvariant() switch
        {
            "AB" => "ABCD",
            "BA" => "BADC",
            "" => "ABCD",
            _ => byteOrder
        };
    }

    private static void EnsureRegisterCount(ushort[] registers, int requiredCount, string parserName)
    {
        if (registers.Length < requiredCount)
        {
            throw new ArgumentException(
                $"{parserName} requires at least {requiredCount} registers, but response contains {registers.Length}.");
        }
    }

    /// <summary>
    /// 应用换算规则
    /// </summary>
    public static double ApplyTransforms(double rawValue, List<ValueTransform> transforms)
    {
        if (transforms == null || transforms.Count == 0)
            return rawValue;

        var result = rawValue;
        foreach (var transform in transforms.OrderBy(t => t.Order))
        {
            result = transform.Type.ToLowerInvariant() switch
            {
                "scale" => result * transform.Parameters.GetValueOrDefault("factor", 1.0),
                "offset" => result + transform.Parameters.GetValueOrDefault("offset", 0.0),
                "clamp" => Math.Clamp(result,
                    transform.Parameters.GetValueOrDefault("min", double.MinValue),
                    transform.Parameters.GetValueOrDefault("max", double.MaxValue)),
                "bitExtract" => ExtractBits(result, transform.Parameters),
                _ => result
            };
        }
        return result;
    }

    /// <summary>
    /// 应用换算规则 (从 JSON)
    /// </summary>
    public static double ApplyTransforms(double rawValue, string transformsJson)
    {
        if (string.IsNullOrEmpty(transformsJson))
            return rawValue;

        try
        {
            var transforms = JsonSerializer.Deserialize<List<ValueTransform>>(transformsJson);
            return ApplyTransforms(rawValue, transforms);
        }
        catch
        {
            return rawValue;
        }
    }

    /// <summary>
    /// 提取位
    /// </summary>
    private static double ExtractBits(double value, Dictionary<string, double> parameters)
    {
        var bitIndex = (int)parameters.GetValueOrDefault("bitIndex", 0);
        var bitCount = (int)parameters.GetValueOrDefault("bitCount", 1);
        var mask = ((1 << bitCount) - 1) << bitIndex;
        return ((ushort)value & mask) >> bitIndex;
    }

    /// <summary>
    /// 解析位字段（从一个寄存器中提取多个位）
    /// </summary>
    public static Dictionary<string, bool> ExtractBitFields(ushort register, Dictionary<string, (int startBit, int bitCount)> fields)
    {
        var result = new Dictionary<string, bool>();
        foreach (var field in fields)
        {
            var mask = (ushort)((1 << field.Value.bitCount) - 1);
            var value = (register >> field.Value.startBit) & mask;
            result[field.Key] = value != 0;
        }
        return result;
    }

    /// <summary>
    /// 枚举映射
    /// </summary>
    public static string ApplyEnumMap(object value, Dictionary<string, string> enumMap)
    {
        var key = value?.ToString() ?? "";
        return enumMap.TryGetValue(key, out var displayName) ? displayName : key;
    }
}

/// <summary>
/// 值换算配置
/// </summary>
public class ValueTransform
{
    public string Type { get; set; }
    public int Order { get; set; }
    public Dictionary<string, double> Parameters { get; set; } = new();
}
