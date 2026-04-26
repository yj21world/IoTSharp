#nullable enable

using IoTSharp.Services.ModbusCollection;
using System;
using Xunit;

namespace IoTSharp.Test
{
    public sealed class ModbusDataParserTests
    {
        [Fact]
        public void ParseRegisters_Float32_Treats_Ab_As_Abcd_For_Two_Register_Value()
        {
            var value = ModbusDataParser.ParseRegisters(
                new byte[] { 0x00, 0x00, 0x00, 0x00 },
                "float32",
                "AB");

            Assert.Equal(0f, Assert.IsType<float>(value));
        }

        [Fact]
        public void ParseRegisters_Float32_Reports_Clear_Error_When_Response_Is_Too_Short()
        {
            var ex = Assert.Throws<ArgumentException>(() => ModbusDataParser.ParseRegisters(
                new byte[] { 0x00, 0x00 },
                "float32",
                "ABCD"));

            Assert.Contains("requires at least 2 registers", ex.Message);
        }
    }
}
