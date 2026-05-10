namespace PowerPilot.Agents;

/// <summary>
/// Configuration options for the energy monitoring background agent.
/// </summary>
public class EnergyMonitoringOptions
{
    /// <summary>
    /// Interval in seconds between energy state checks (default: 60 seconds).
    /// </summary>
    public int MonitoringIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Minimum net power production in kW to trigger a notification (default: 2.0 kW).
    /// </summary>
    public decimal EnergyThresholdKw { get; set; } = 2.0m;

    /// <summary>
    /// Duration in minutes that the threshold must be sustained before triggering (default: 2 minutes).
    /// </summary>
    public int ThresholdDurationMinutes { get; set; } = 2;

    /// <summary>
    /// Minimum time in minutes between notifications to prevent spam (default: 15 minutes).
    /// </summary>
    public int NotificationCooldownMinutes { get; set; } = 15;

    /// <summary>
    /// Dictionary of subagent names and their enabled status.
    /// Available agents: "energy_analyzer", "appliance_advisor", "timing_optimizer"
    /// </summary>
    public Dictionary<string, bool> EnabledSubAgents { get; set; } = new()
    {
        { "energy_analyzer", true },
        { "appliance_advisor", true },
        { "timing_optimizer", true }
    };
}
