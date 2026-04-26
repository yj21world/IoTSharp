using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Protocol;
using MQTTnet.Server;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace IoTSharp.Services.ModbusCollection;

/// <summary>
/// Modbus MQTT 传输层，负责 Topic 发布与响应接入。
/// </summary>
public sealed class ModbusMqttTransport
{
    private const int MaxPayloadLength = 4096;

    private readonly MqttServer _mqttServer;
    private readonly ILogger<ModbusMqttTransport> _logger;
    private readonly Channel<ModbusMqttResponse> _responses;
    private bool _started;

    public ModbusMqttTransport(MqttServer mqttServer, ILogger<ModbusMqttTransport> logger)
    {
        _mqttServer = mqttServer;
        _logger = logger;
        _responses = Channel.CreateBounded<ModbusMqttResponse>(new BoundedChannelOptions(1024)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });
    }

    public ChannelReader<ModbusMqttResponse> Responses => _responses.Reader;

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _mqttServer.InterceptingPublishAsync += HandleMqttMessageAsync;
        _started = true;
        _logger.LogInformation("Modbus MQTT transport started, listening on {TopicFilter}", ModbusTopic.ResponseTopicFilter);
    }

    public void Stop()
    {
        if (!_started)
        {
            return;
        }

        _mqttServer.InterceptingPublishAsync -= HandleMqttMessageAsync;
        _started = false;
        _responses.Writer.TryComplete();
        _logger.LogInformation("Modbus MQTT transport stopped");
    }

    public async Task PublishRequestAsync(string gatewayName, string requestId, string hexPayload)
    {
        var topic = ModbusTopic.BuildRequestTopic(gatewayName, requestId);
        await _mqttServer.InjectApplicationMessage(
            topic,
            hexPayload,
            MqttQualityOfServiceLevel.AtMostOnce,
            false);
    }

    private Task HandleMqttMessageAsync(InterceptingPublishEventArgs e)
    {
        var message = e.ApplicationMessage;
        var topic = message.Topic;

        if (!ModbusTopic.TryParseResponseTopic(topic, out var gatewayName))
        {
            return Task.CompletedTask;
        }

        var payload = message.ConvertPayloadToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(payload))
        {
            _logger.LogDebug("Ignore empty Modbus response payload for topic {Topic}", topic);
            return Task.CompletedTask;
        }

        if (payload.Length > MaxPayloadLength)
        {
            _logger.LogWarning("Ignore oversized Modbus response for topic {Topic}, length={Length}", topic, payload.Length);
            return Task.CompletedTask;
        }

        var response = new ModbusMqttResponse
        {
            GatewayName = gatewayName,
            Payload = payload
        };

        if (!_responses.Writer.TryWrite(response))
        {
            _logger.LogWarning(
                "Drop Modbus response because queue is full: gateway={Gateway}",
                gatewayName);
        }

        return Task.CompletedTask;
    }
}

public sealed class ModbusMqttResponse
{
    public string GatewayName { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
}
