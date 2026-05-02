using PowerPilot.Core.Interfaces;
using PowerPilot.Core.Models;

namespace PowerPilot.Web.Services;

public class P1BackgroundService : BackgroundService
{
    private readonly IP1Reader _reader;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EnergyStateService _stateService;
    private readonly ILogger<P1BackgroundService> _logger;
    private int _readingCounter;

    public P1BackgroundService(IP1Reader reader, IServiceScopeFactory scopeFactory, EnergyStateService stateService, ILogger<P1BackgroundService> logger)
    {
        _reader = reader;
        _scopeFactory = scopeFactory;
        _stateService = stateService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _reader.TelegramReceived += OnTelegramReceived;
        await _reader.StartAsync(stoppingToken);
        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) { }
        await _reader.StopAsync();
        _reader.TelegramReceived -= OnTelegramReceived;
    }

    private void OnTelegramReceived(object? sender, P1Telegram telegram)
    {
        _stateService.Update(telegram);
        if (++_readingCounter % 6 == 0)
            _ = PersistReadingAsync(telegram);
    }

    private async Task PersistReadingAsync(P1Telegram telegram)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IEnergyRepository>();
            await repository.SaveReadingAsync(new EnergyReading
            {
                Timestamp = telegram.Timestamp,
                ElectricityDeliveredTariff1 = telegram.ElectricityDeliveredTariff1,
                ElectricityDeliveredTariff2 = telegram.ElectricityDeliveredTariff2,
                ElectricityReturnedTariff1 = telegram.ElectricityReturnedTariff1,
                ElectricityReturnedTariff2 = telegram.ElectricityReturnedTariff2,
                CurrentPowerUsage = telegram.CurrentPowerUsage,
                CurrentPowerDelivery = telegram.CurrentPowerDelivery,
                GasDelivered = telegram.GasDelivered
            });
        }
        catch (Exception ex) { _logger.LogError(ex, "Failed to persist energy reading"); }
    }
}
