using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PowerPilot.Core.Interfaces;
using PowerPilot.Core.Models;

namespace PowerPilot.P1Reader;

public class HomeWizardOptions
{
    public string IpAddress { get; set; } = "";
    public int PollingIntervalSeconds { get; set; } = 10;
}

public class HomeWizardP1Reader : IP1Reader
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HomeWizardOptions _options;
    private readonly ILogger<HomeWizardP1Reader> _logger;
    private CancellationTokenSource? _cts;
    private Task? _pollingTask;
    private bool _isConnected;

    public event EventHandler<P1Telegram>? TelegramReceived;
    public bool IsConnected => _isConnected;

    public HomeWizardP1Reader(IHttpClientFactory httpClientFactory, IOptions<HomeWizardOptions> options, ILogger<HomeWizardP1Reader> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pollingTask = Task.Run(() => PollAsync(_cts.Token), _cts.Token);
        _logger.LogInformation("HomeWizard P1 reader started (polling {IpAddress})", _options.IpAddress);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _cts?.Cancel();
        if (_pollingTask != null)
        {
            try { await _pollingTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (TimeoutException) { }
        }
        _isConnected = false;
        _logger.LogInformation("HomeWizard P1 reader stopped");
    }

    private async Task PollAsync(CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        var url = $"http://{_options.IpAddress}/api/v1/data";

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var measurement = await client.GetFromJsonAsync<HomeWizardMeasurement>(url, ct);
                if (measurement is not null)
                {
                    _isConnected = true;
                    TelegramReceived?.Invoke(this, MapToTelegram(measurement));
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _isConnected = false;
                _logger.LogWarning(ex, "Failed to poll HomeWizard at {IpAddress}", _options.IpAddress);
            }

            try { await Task.Delay(TimeSpan.FromSeconds(_options.PollingIntervalSeconds), ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private static P1Telegram MapToTelegram(HomeWizardMeasurement m)
    {
        var powerW = m.ActivePowerW ?? 0m;

        return new P1Telegram
        {
            Timestamp = DateTime.UtcNow,
            EquipmentIdentifier = m.UniqueId ?? "",
            // V1 API has no per-tariff breakdown; total mapped to T1
            ElectricityDeliveredTariff1 = m.TotalPowerImportKwh ?? 0m,
            ElectricityDeliveredTariff2 = 0m,
            ElectricityReturnedTariff1 = m.TotalPowerExportKwh ?? 0m,
            ElectricityReturnedTariff2 = 0m,
            CurrentTariff = 0,
            CurrentPowerUsage = powerW > 0 ? powerW / 1000m : 0m,
            CurrentPowerDelivery = powerW < 0 ? Math.Abs(powerW) / 1000m : 0m,
            GasDelivered = m.TotalGasM3 ?? 0m
        };
    }
}

internal sealed class HomeWizardMeasurement
{
    [JsonPropertyName("unique_id")] public string? UniqueId { get; set; }
    [JsonPropertyName("total_power_import_kwh")] public decimal? TotalPowerImportKwh { get; set; }
    [JsonPropertyName("total_power_export_kwh")] public decimal? TotalPowerExportKwh { get; set; }
    [JsonPropertyName("active_power_w")] public decimal? ActivePowerW { get; set; }
    [JsonPropertyName("total_gas_m3")] public decimal? TotalGasM3 { get; set; }
}
