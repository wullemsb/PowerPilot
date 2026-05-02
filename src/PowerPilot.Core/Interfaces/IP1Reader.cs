namespace PowerPilot.Core.Interfaces;

using PowerPilot.Core.Models;

public interface IP1Reader
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    event EventHandler<P1Telegram>? TelegramReceived;
    bool IsConnected { get; }
}
