using Microsoft.Extensions.Logging.Abstractions;
using PowerPilot.Core.Models;
using PowerPilot.P1Reader;
using Xunit;

namespace PowerPilot.Tests;

public class SimulatedP1ReaderTests
{
    [Fact]
    public async Task StartAsync_SetsIsConnectedTrue()
    {
        var reader = new SimulatedP1Reader(NullLogger<SimulatedP1Reader>.Instance);
        Assert.False(reader.IsConnected);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await reader.StartAsync(cts.Token);
        Assert.True(reader.IsConnected);
        await reader.StopAsync();
        Assert.False(reader.IsConnected);
    }

    [Fact]
    public void P1Telegram_NetPower_Calculation_IsCorrect()
    {
        var telegram = new P1Telegram { CurrentPowerUsage = 1.5m, CurrentPowerDelivery = 2.0m };
        Assert.Equal(0.5m, telegram.NetPower);
        Assert.True(telegram.IsProducing);
    }

    [Fact]
    public void P1Telegram_NetPower_Consuming_IsNegative()
    {
        var telegram = new P1Telegram { CurrentPowerUsage = 2.0m, CurrentPowerDelivery = 0m };
        Assert.Equal(-2.0m, telegram.NetPower);
        Assert.False(telegram.IsProducing);
    }
}
