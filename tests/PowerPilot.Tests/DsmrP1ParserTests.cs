using PowerPilot.P1Reader;
using Xunit;

namespace PowerPilot.Tests;

public class DsmrP1ParserTests
{
    private const string SampleTelegram = @"/ISk5\2MT382-1000
0-0:96.1.1(4B384547303034303436333935353037)
1-0:1.8.1(000671.578*kWh)
1-0:1.8.2(000842.402*kWh)
1-0:2.8.1(000000.000*kWh)
1-0:2.8.2(000762.991*kWh)
0-0:96.14.0(0002)
1-0:1.7.0(01.156*kW)
1-0:2.7.0(00.000*kW)
0-1:24.2.1(101209112500W)(12785.123*m3)
!2F3A";

    [Fact]
    public void Parse_ValidTelegram_ReturnsCorrectValues()
    {
        var result = DsmrP1Parser.Parse(SampleTelegram);
        Assert.NotNull(result);
        Assert.Equal(671.578m, result.ElectricityDeliveredTariff1);
        Assert.Equal(842.402m, result.ElectricityDeliveredTariff2);
        Assert.Equal(0m, result.ElectricityReturnedTariff1);
        Assert.Equal(762.991m, result.ElectricityReturnedTariff2);
        Assert.Equal(2, result.CurrentTariff);
        Assert.Equal(1.156m, result.CurrentPowerUsage);
        Assert.Equal(0m, result.CurrentPowerDelivery);
        Assert.Equal(12785.123m, result.GasDelivered);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsNull()
    {
        Assert.Null(DsmrP1Parser.Parse(string.Empty));
    }

    [Fact]
    public void Parse_NullInput_ReturnsNull()
    {
        Assert.Null(DsmrP1Parser.Parse(null!));
    }

    [Fact]
    public void Parse_ProducingTelegram_NetPowerIsPositive()
    {
        var telegram = "/ISk5\\2MT382-1000\n1-0:1.7.0(00.000*kW)\n1-0:2.7.0(02.500*kW)\n!ABCD";
        var result = DsmrP1Parser.Parse(telegram);
        Assert.NotNull(result);
        Assert.True(result.IsProducing);
        Assert.Equal(2.5m, result.NetPower);
    }

    [Fact]
    public void Parse_ConsumingTelegram_NetPowerIsNegative()
    {
        var telegram = "/ISk5\\2MT382-1000\n1-0:1.7.0(01.500*kW)\n1-0:2.7.0(00.000*kW)\n!ABCD";
        var result = DsmrP1Parser.Parse(telegram);
        Assert.NotNull(result);
        Assert.False(result.IsProducing);
        Assert.Equal(-1.5m, result.NetPower);
    }
}
