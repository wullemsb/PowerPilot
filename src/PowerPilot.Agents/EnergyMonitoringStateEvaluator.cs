using PowerPilot.Core.Models;

namespace PowerPilot.Agents;

internal sealed class EnergyMonitoringStateEvaluator
{
    private readonly EnergyMonitoringOptions _options;

    public EnergyMonitoringStateEvaluator(EnergyMonitoringOptions options)
    {
        _options = options;
    }

    public EnergyMonitoringEvaluation Evaluate(
        P1Telegram? telegram,
        DateTime utcNow,
        DateTime? thresholdExceededSince,
        DateTime? lastNotificationTime)
    {
        var requiredDuration = TimeSpan.FromMinutes(_options.ThresholdDurationMinutes);
        var cooldownPeriod = TimeSpan.FromMinutes(_options.NotificationCooldownMinutes);

        if (telegram == null)
        {
            return new EnergyMonitoringEvaluation(
                HasTelegram: false,
                ExceedsThreshold: false,
                RequiredDurationMet: false,
                CooldownElapsed: false,
                ShouldNotify: false,
                ThresholdExceededSince: thresholdExceededSince,
                ExceededDuration: TimeSpan.Zero,
                RequiredDuration: requiredDuration,
                CooldownRemaining: TimeSpan.Zero);
        }

        var exceedsThreshold = telegram.IsProducing && telegram.NetPower >= _options.EnergyThresholdKw;
        if (!exceedsThreshold)
        {
            return new EnergyMonitoringEvaluation(
                HasTelegram: true,
                ExceedsThreshold: false,
                RequiredDurationMet: false,
                CooldownElapsed: false,
                ShouldNotify: false,
                ThresholdExceededSince: null,
                ExceededDuration: TimeSpan.Zero,
                RequiredDuration: requiredDuration,
                CooldownRemaining: TimeSpan.Zero);
        }

        var effectiveThresholdExceededSince = thresholdExceededSince ?? utcNow;
        var exceededDuration = utcNow - effectiveThresholdExceededSince;
        if (exceededDuration < requiredDuration)
        {
            return new EnergyMonitoringEvaluation(
                HasTelegram: true,
                ExceedsThreshold: true,
                RequiredDurationMet: false,
                CooldownElapsed: false,
                ShouldNotify: false,
                ThresholdExceededSince: effectiveThresholdExceededSince,
                ExceededDuration: exceededDuration,
                RequiredDuration: requiredDuration,
                CooldownRemaining: TimeSpan.Zero);
        }

        var timeSinceLastNotification = lastNotificationTime.HasValue
            ? utcNow - lastNotificationTime.Value
            : TimeSpan.MaxValue;
        var cooldownElapsed = timeSinceLastNotification >= cooldownPeriod;
        var cooldownRemaining = cooldownElapsed
            ? TimeSpan.Zero
            : cooldownPeriod - timeSinceLastNotification;

        return new EnergyMonitoringEvaluation(
            HasTelegram: true,
            ExceedsThreshold: true,
            RequiredDurationMet: true,
            CooldownElapsed: cooldownElapsed,
            ShouldNotify: cooldownElapsed,
            ThresholdExceededSince: cooldownElapsed ? null : effectiveThresholdExceededSince,
            ExceededDuration: exceededDuration,
            RequiredDuration: requiredDuration,
            CooldownRemaining: cooldownRemaining);
    }
}
