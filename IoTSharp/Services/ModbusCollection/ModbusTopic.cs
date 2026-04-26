namespace IoTSharp.Services.ModbusCollection;

/// <summary>
/// Modbus MQTT Topic 协议
/// </summary>
public static class ModbusTopic
{
    public const string ResponseTopicFilter = "gateway/+/modbus/response";

    public static string BuildRequestTopic(string gatewayName, string requestId)
    {
        return $"gateway/{gatewayName}/modbus/request/{requestId}";
    }

    public static string BuildResponseTopic(string gatewayName)
    {
        return $"gateway/{gatewayName}/modbus/response";
    }

    public static bool TryParseResponseTopic(string topic, out string gatewayName)
    {
        gatewayName = string.Empty;

        if (string.IsNullOrWhiteSpace(topic) || topic[0] == '/')
        {
            return false;
        }

        var parts = topic.Split('/');
        if (parts.Length != 4)
        {
            return false;
        }

        if (!string.Equals(parts[0], "gateway", System.StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(parts[2], "modbus", System.StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(parts[3], "response", System.StringComparison.Ordinal))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(parts[1]))
        {
            return false;
        }

        gatewayName = parts[1];
        return true;
    }
}
