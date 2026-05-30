using PowerPilot.Agents;
using PowerPilot.Core.Models;

namespace PowerPilot.Tests;

public sealed class EnergyMonitoringStateEvaluatorTests
{
    private static readonly DateTime BaseTime = new(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Evaluate_StartsThresholdTimer_WhenSurplusFirstExceedsThreshold()
    {
        var evaluator = CreateEvaluator();

        var result = evaluator.Evaluate(
            CreateProducingTelegram(2.5m),
            BaseTime,
            thresholdExceededSince: null,
            lastNotificationTime: null);

        Assert.True(result.HasTelegram);
        Assert.True(result.ExceedsThreshold);
        Assert.False(result.RequiredDurationMet);
        Assert.False(result.ShouldNotify);
        Assert.Equal(BaseTime, result.ThresholdExceededSince);
    }

    [Fact]
    public void Evaluate_TriggersNotification_WhenThresholdDurationAndCooldownAreSatisfied()
    {
        var evaluator = CreateEvaluator();

        var result = evaluator.Evaluate(
            CreateProducingTelegram(2.5m),
            BaseTime.AddMinutes(3),
            thresholdExceededSince: BaseTime,
            lastNotificationTime: BaseTime.AddMinutes(-10));

        Assert.True(result.RequiredDurationMet);
        Assert.True(result.CooldownElapsed);
        Assert.True(result.ShouldNotify);
        Assert.Null(result.ThresholdExceededSince);
    }

    [Fact]
    public void Evaluate_DoesNotNotifyDuringCooldown_AndKeepsThresholdStart()
    {
        var evaluator = CreateEvaluator();

        var result = evaluator.Evaluate(
            CreateProducingTelegram(2.5m),
            BaseTime.AddMinutes(3),
            thresholdExceededSince: BaseTime,
            lastNotificationTime: BaseTime.AddMinutes(1));

        Assert.True(result.RequiredDurationMet);
        Assert.False(result.CooldownElapsed);
        Assert.False(result.ShouldNotify);
        Assert.Equal(BaseTime, result.ThresholdExceededSince);
        Assert.Equal(TimeSpan.FromMinutes(3), result.CooldownRemaining);
    }

    private static EnergyMonitoringStateEvaluator CreateEvaluator() =>
        new(new EnergyMonitoringOptions
        {
            EnergyThresholdKw = 2m,
            ThresholdDurationMinutes = 2,
            NotificationCooldownMinutes = 5
        });

    private static P1Telegram CreateProducingTelegram(decimal netPowerKw) =>
        new()
        {
            CurrentPowerUsage = 0m,
            CurrentPowerDelivery = netPowerKw
        };
}
