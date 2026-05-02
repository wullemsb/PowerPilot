namespace PowerPilot.Core.Interfaces;

using PowerPilot.Core.Models;

public interface IEnergyStateService
{
    P1Telegram? CurrentTelegram { get; }
    event EventHandler<P1Telegram>? StateUpdated;
}
