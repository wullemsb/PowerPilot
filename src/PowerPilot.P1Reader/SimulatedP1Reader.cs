using Microsoft.Extensions.Logging;
using PowerPilot.Core.Interfaces;
using PowerPilot.Core.Models;

namespace PowerPilot.P1Reader;

public class SimulatedP1Reader : IP1Reader
{
    private readonly ILogger<SimulatedP1Reader> _logger;
    private CancellationTokenSource? _cts;
    private Task? _simulationTask;
    private bool _isConnected;

    private decimal _deliveredT1 = 1234.567m;
    private decimal _deliveredT2 = 2345.678m;
    private decimal _returnedT1 = 123.456m;
    private decimal _returnedT2 = 234.567m;
    private decimal _gasDelivered = 543.210m;

    public event EventHandler<P1Telegram>? TelegramReceived;
    public bool IsConnected => _isConnected;

    public SimulatedP1Reader(ILogger<SimulatedP1Reader> logger) { _logger = logger; }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _isConnected = true;
        _simulationTask = Task.Run(() => SimulateAsync(_cts.Token), _cts.Token);
        _logger.LogInformation("Simulated P1 reader started");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _cts?.Cancel();
        if (_simulationTask != null)
        {
            try { await _simulationTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (TimeoutException) { }
        }
        _isConnected = false;
        _logger.LogInformation("Simulated P1 reader stopped");
    }

    private async Task SimulateAsync(CancellationToken ct)
    {
        var rng = new Random();
        while (!ct.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var hour = now.Hour;
            var solarFactor = hour >= 7 && hour <= 19
                ? Math.Sin((hour - 7) * Math.PI / 12.0) * (0.8 + rng.NextDouble() * 0.4)
                : 0;
            var solarProduction = (decimal)(solarFactor * 3.5);
            var baseDemand = hour >= 7 && hour <= 22 ? 0.5 + rng.NextDouble() * 1.5 : 0.1 + rng.NextDouble() * 0.3;
            var consumption = (decimal)baseDemand;
            var netPower = solarProduction - consumption;
            var currentPowerUsage = netPower < 0 ? Math.Abs(netPower) : 0m;
            var currentPowerDelivery = netPower > 0 ? netPower : 0m;
            var intervalHours = 10m / 3600m;
            var tariff = hour >= 22 || hour < 7 ? 1 : 2;

            if (currentPowerUsage > 0) { if (tariff == 1) _deliveredT1 += currentPowerUsage * intervalHours; else _deliveredT2 += currentPowerUsage * intervalHours; }
            if (currentPowerDelivery > 0) { if (tariff == 1) _returnedT1 += currentPowerDelivery * intervalHours; else _returnedT2 += currentPowerDelivery * intervalHours; }
            _gasDelivered += (decimal)(rng.NextDouble() * 0.0001);

            var telegram = new P1Telegram
            {
                Timestamp = DateTime.UtcNow,
                EquipmentIdentifier = "SIM001",
                ElectricityDeliveredTariff1 = _deliveredT1,
                ElectricityDeliveredTariff2 = _deliveredT2,
                ElectricityReturnedTariff1 = _returnedT1,
                ElectricityReturnedTariff2 = _returnedT2,
                CurrentTariff = tariff,
                CurrentPowerUsage = currentPowerUsage,
                CurrentPowerDelivery = currentPowerDelivery,
                GasDelivered = _gasDelivered
            };
            TelegramReceived?.Invoke(this, telegram);

            try { await Task.Delay(10_000, ct); }
            catch (OperationCanceledException) { break; }
        }
    }
}
