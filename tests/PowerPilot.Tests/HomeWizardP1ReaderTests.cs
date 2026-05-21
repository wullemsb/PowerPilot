using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PowerPilot.Core.Models;
using PowerPilot.P1Reader;

namespace PowerPilot.Tests;

public class HomeWizardP1ReaderTests
{
    private static HomeWizardP1Reader CreateReader(HttpMessageHandler handler, int pollingIntervalSeconds = 0)
    {
        var httpClient = new HttpClient(handler);
        var factory = new FakeHttpClientFactory(httpClient);
        var options = Options.Create(new HomeWizardOptions
        {
            IpAddress = "192.168.1.100",
            PollingIntervalSeconds = pollingIntervalSeconds
        });
        return new HomeWizardP1Reader(factory, options, NullLogger<HomeWizardP1Reader>.Instance);
    }

    [Fact]
    public void IsConnected_IsFalseBeforeStart()
    {
        var reader = CreateReader(new FailingHttpMessageHandler());
        Assert.False(reader.IsConnected);
    }

    [Fact]
    public async Task IsConnected_BecomesTrue_AfterSuccessfulPoll()
    {
        var tcs = new TaskCompletionSource<P1Telegram>();
        var reader = CreateReader(new JsonHttpMessageHandler("""{"active_power_w": 0}"""));
        reader.TelegramReceived += (_, t) => tcs.TrySetResult(t);

        await reader.StartAsync();
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(reader.IsConnected);
        await reader.StopAsync();
    }

    [Fact]
    public async Task StopAsync_SetsIsConnectedFalse()
    {
        var tcs = new TaskCompletionSource<P1Telegram>();
        var reader = CreateReader(new JsonHttpMessageHandler("""{"active_power_w": 100}"""));
        reader.TelegramReceived += (_, t) => tcs.TrySetResult(t);

        await reader.StartAsync();
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(reader.IsConnected);

        await reader.StopAsync();
        Assert.False(reader.IsConnected);
    }

    [Fact]
    public async Task IsConnected_StaysFalse_WhenHttpCallFails()
    {
        var reader = CreateReader(new FailingHttpMessageHandler());

        await reader.StartAsync();
        await Task.Delay(150);
        await reader.StopAsync();

        Assert.False(reader.IsConnected);
    }

    [Fact]
    public async Task TelegramReceived_IsRaised_OnSuccessfulPoll()
    {
        var tcs = new TaskCompletionSource<P1Telegram>();
        var reader = CreateReader(new JsonHttpMessageHandler("""{"active_power_w": 500}"""));
        reader.TelegramReceived += (_, t) => tcs.TrySetResult(t);

        await reader.StartAsync();
        var telegram = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await reader.StopAsync();

        Assert.NotNull(telegram);
    }

    [Fact]
    public async Task TelegramReceived_MapsPositiveActivePower_ToCurrentPowerUsage()
    {
        var tcs = new TaskCompletionSource<P1Telegram>();
        var reader = CreateReader(new JsonHttpMessageHandler("""{"active_power_w": 2500}"""));
        reader.TelegramReceived += (_, t) => tcs.TrySetResult(t);

        await reader.StartAsync();
        var telegram = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await reader.StopAsync();

        Assert.Equal(2.5m, telegram.CurrentPowerUsage);
        Assert.Equal(0m, telegram.CurrentPowerDelivery);
    }

    [Fact]
    public async Task TelegramReceived_MapsNegativeActivePower_ToCurrentPowerDelivery()
    {
        var tcs = new TaskCompletionSource<P1Telegram>();
        var reader = CreateReader(new JsonHttpMessageHandler("""{"active_power_w": -1800}"""));
        reader.TelegramReceived += (_, t) => tcs.TrySetResult(t);

        await reader.StartAsync();
        var telegram = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await reader.StopAsync();

        Assert.Equal(0m, telegram.CurrentPowerUsage);
        Assert.Equal(1.8m, telegram.CurrentPowerDelivery);
    }

    [Fact]
    public async Task TelegramReceived_ZeroActivePower_BothUsageAndDeliveryAreZero()
    {
        var tcs = new TaskCompletionSource<P1Telegram>();
        var reader = CreateReader(new JsonHttpMessageHandler("""{"active_power_w": 0}"""));
        reader.TelegramReceived += (_, t) => tcs.TrySetResult(t);

        await reader.StartAsync();
        var telegram = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await reader.StopAsync();

        Assert.Equal(0m, telegram.CurrentPowerUsage);
        Assert.Equal(0m, telegram.CurrentPowerDelivery);
    }

    [Fact]
    public async Task TelegramReceived_MapsAllFields_Correctly()
    {
        var json = """
            {
                "unique_id": "HW-ABC123",
                "total_power_import_kwh": 1234.567,
                "total_power_export_kwh": 890.123,
                "active_power_w": 500,
                "total_gas_m3": 456.789
            }
            """;
        var tcs = new TaskCompletionSource<P1Telegram>();
        var reader = CreateReader(new JsonHttpMessageHandler(json));
        reader.TelegramReceived += (_, t) => tcs.TrySetResult(t);

        await reader.StartAsync();
        var telegram = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await reader.StopAsync();

        Assert.Equal("HW-ABC123", telegram.EquipmentIdentifier);
        Assert.Equal(1234.567m, telegram.ElectricityDeliveredTariff1);
        Assert.Equal(0m, telegram.ElectricityDeliveredTariff2);
        Assert.Equal(890.123m, telegram.ElectricityReturnedTariff1);
        Assert.Equal(0m, telegram.ElectricityReturnedTariff2);
        Assert.Equal(0.5m, telegram.CurrentPowerUsage);
        Assert.Equal(0m, telegram.CurrentPowerDelivery);
        Assert.Equal(456.789m, telegram.GasDelivered);
    }

    [Fact]
    public async Task TelegramReceived_HandlesNullFields_WithZeroDefaults()
    {
        var tcs = new TaskCompletionSource<P1Telegram>();
        var reader = CreateReader(new JsonHttpMessageHandler("{}"));
        reader.TelegramReceived += (_, t) => tcs.TrySetResult(t);

        await reader.StartAsync();
        var telegram = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await reader.StopAsync();

        Assert.Equal("", telegram.EquipmentIdentifier);
        Assert.Equal(0m, telegram.ElectricityDeliveredTariff1);
        Assert.Equal(0m, telegram.ElectricityReturnedTariff1);
        Assert.Equal(0m, telegram.CurrentPowerUsage);
        Assert.Equal(0m, telegram.CurrentPowerDelivery);
        Assert.Equal(0m, telegram.GasDelivered);
    }

    private sealed class JsonHttpMessageHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    private sealed class FailingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromException<HttpResponseMessage>(new HttpRequestException("Simulated network failure"));
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;
        public FakeHttpClientFactory(HttpClient client) => _client = client;
        public HttpClient CreateClient(string name) => _client;
    }
}
