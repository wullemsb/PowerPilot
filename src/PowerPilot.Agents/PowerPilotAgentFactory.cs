using System.Collections.ObjectModel;
using System.ComponentModel;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using PowerPilot.Agents.Plugins;

namespace PowerPilot.Agents;

public static class PowerPilotAgentFactory
{
    /// <summary>
    /// Creates and configures a <see cref="CopilotClient"/> using the provided options.
    /// The client uses the GitHub Copilot CLI — either the bundled binary or the one
    /// at <c>COPILOT_CLI_PATH</c> — to access GitHub Copilot models.
    /// </summary>
    public static CopilotClient CreateClient(AgentOptions options, ILogger? logger = null)
    {
        var clientOptions = new CopilotClientOptions();

        if (!string.IsNullOrEmpty(options.GitHubToken))
            clientOptions.GitHubToken = options.GitHubToken;

        if (!string.IsNullOrEmpty(options.CliPath))
            clientOptions.CliPath = options.CliPath;

        if (logger != null)
            clientOptions.Logger = logger;

        return new CopilotClient(clientOptions);
    }

    /// <summary>
    /// Builds the list of <see cref="AIFunction"/> tools that are registered with each
    /// Copilot session.  All tools are marked <c>skip_permission</c> because they are
    /// read-only data lookups that require no user confirmation.
    /// </summary>
    public static IReadOnlyList<AIFunction> BuildTools(
        EnergyPlugin energyPlugin,
        WeatherPlugin weatherPlugin)
    {
        // All our tools are safe read-only lookups — skip the permission prompt.
        var skipPermission = new ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?> { ["skip_permission"] = true });

        AIFunction Tool(Delegate fn, string name, string description) =>
            AIFunctionFactory.Create(fn, new AIFunctionFactoryOptions
            {
                Name = name,
                Description = description,
                AdditionalProperties = skipPermission,
            });

        return new List<AIFunction>
        {
            Tool(() => energyPlugin.GetCurrentPower(),
                "get_current_power",
                "Get the current real-time power consumption and production in kilowatts"),

            Tool(() => energyPlugin.GetTodayStatsAsync(),
                "get_today_stats",
                "Get energy consumption and production statistics for today"),

            Tool(([Description("Time period: today, yesterday, week, or month")] string period = "today")
                    => energyPlugin.GetEnergyStatsAsync(period),
                "get_energy_stats",
                "Get energy statistics for a given time period"),

            Tool(() => energyPlugin.GetHourlyProfileAsync(),
                "get_hourly_profile",
                "Get the hourly energy profile for today to understand usage patterns"),

            Tool(([Description("Appliance name, e.g. dishwasher, washing machine, dryer, EV charger")] string appliance)
                    => energyPlugin.GetApplianceAdviceAsync(appliance),
                "get_appliance_advice",
                "Get advice on the best time to run a high-power appliance based on current and historical production data"),

            Tool(() => weatherPlugin.GetCurrentWeatherAsync(),
                "get_current_weather",
                "Get current weather including temperature, cloud cover, and estimated solar irradiance"),

            Tool(() => weatherPlugin.GetSolarForecastAsync(),
                "get_solar_forecast",
                "Get the solar production forecast for the next 24 hours based on weather data"),
        }.AsReadOnly();
    }
}
