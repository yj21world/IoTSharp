#nullable enable

using IoTSharp.Services.ModbusCollection;
using Xunit;

namespace IoTSharp.Test
{
    public sealed class ModbusTopicTests
    {
        [Fact]
        public void BuildRequestTopic_Uses_Expected_Format()
        {
            var topic = ModbusTopic.BuildRequestTopic("test2", "req001");

            Assert.Equal("gateway/test2/modbus/request/req001", topic);
        }

        [Fact]
        public void TryParseResponseTopic_Parses_Valid_Topic()
        {
            var ok = ModbusTopic.TryParseResponseTopic(
                "gateway/test2/modbus/response",
                out var gatewayName);

            Assert.True(ok);
            Assert.Equal("test2", gatewayName);
        }

        [Fact]
        public void TryParseResponseTopic_Rejects_Leading_Slash()
        {
            var ok = ModbusTopic.TryParseResponseTopic(
                "/gateway/test2/modbus/response",
                out _);

            Assert.False(ok);
        }

        [Fact]
        public void TryParseResponseTopic_Rejects_Request_Topic()
        {
            var ok = ModbusTopic.TryParseResponseTopic(
                "gateway/test2/modbus/request/req001",
                out _);

            Assert.False(ok);
        }

        [Fact]
        public void TryParseResponseTopic_Rejects_Dynamic_Response_Topic()
        {
            var ok = ModbusTopic.TryParseResponseTopic(
                "gateway/test2/modbus/response/req001",
                out _);

            Assert.False(ok);
        }
    }
}
