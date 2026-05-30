namespace PowerPilot.Agents;

internal sealed record EnergyMonitoringEvaluation(
    bool HasTelegram,
    bool ExceedsThreshold,
    bool RequiredDurationMet,
    bool CooldownElapsed,
    bool ShouldNotify,
    DateTime? ThresholdExceededSince,
    TimeSpan ExceededDuration,
    TimeSpan RequiredDuration,
    TimeSpan CooldownRemaining);
