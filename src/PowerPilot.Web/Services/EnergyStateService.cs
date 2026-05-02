using PowerPilot.Core.Interfaces;
using PowerPilot.Core.Models;

namespace PowerPilot.Web.Services;

public class EnergyStateService : IEnergyStateService
{
    private P1Telegram? _currentTelegram;
    public P1Telegram? CurrentTelegram => _currentTelegram;
    public event EventHandler<P1Telegram>? StateUpdated;

    public void Update(P1Telegram telegram)
    {
        _currentTelegram = telegram;
        StateUpdated?.Invoke(this, telegram);
    }
}
