using System.Collections.ObjectModel;
using System.ComponentModel;
using GitHub.Copilot;
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
        //This should be the default
        clientOptions.Connection = RuntimeConnection.ForStdio();

        if (!string.IsNullOrEmpty(options.GitHubToken))
            clientOptions.GitHubToken = options.GitHubToken;

        if (!string.IsNullOrEmpty(options.CliPath))
        {
            clientOptions.Connection = RuntimeConnection.ForStdio(options.CliPath);
        }

        if (!string.IsNullOrEmpty(options.CliUrl))
        {
            clientOptions.Connection = RuntimeConnection.ForUri(options.CliUrl);
        }

        if (logger != null)
            clientOptions.Logger = logger;

        clientOptions.Telemetry = new TelemetryConfig
        {
            OtlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT"),
            CaptureContent = true,
            ExporterType = "oltp-http",
        };
        //Export to file
        //clientOptions.Telemetry = new TelemetryConfig
        //{
        //    FilePath = "copilot_client_telemetry.log",
        //    CaptureContent = true,
        //    ExporterType = "file",
        //   
        //};

        return new CopilotClient(clientOptions);
    }

    /// <summary>
    /// Builds the list of <see cref="AIFunction"/> tools that are registered with each
    /// Copilot session.  All tools are marked <c>skip_permission</c> because they are
    /// read-only data lookups that require no user confirmation.
    /// </summary>
    public static IReadOnlyList<AIFunctionDeclaration> BuildTools(
        EnergyPlugin energyPlugin,
        WeatherPlugin weatherPlugin)
    {
        // All our tools are safe read-only lookups — skip the permission prompt.
        var toolOptions = new CopilotToolOptions
        {
            SkipPermission = true,
        };

        return new List<AIFunction>
        {
            CopilotTool.DefineTool(()=> energyPlugin.GetCurrentPower(),
                toolOptions,new AIFunctionFactoryOptions() 
                { 
                    Name = "get_current_power", 
                    Description= "Get the current real-time power consumption and production in kilowatts" 
                }),
            CopilotTool.DefineTool(()=> energyPlugin.GetTodayStatsAsync(),toolOptions, new AIFunctionFactoryOptions()
                {
                    Name = "get_today_stats",
                    Description= "Get energy consumption and production statistics for today"
                }),
            CopilotTool.DefineTool(([Description("Time period: today, yesterday, week, or month")] string period = "today")
                => energyPlugin.GetEnergyStatsAsync(period),toolOptions, new AIFunctionFactoryOptions()
                {
                    Name = "get_energy_stats",
                    Description= "Get energy statistics for a given time period"
                }),
            CopilotTool.DefineTool(() =>energyPlugin.GetHourlyProfileAsync(), toolOptions, new AIFunctionFactoryOptions()
                {
                    Name = "get_hourly_profile",
                    Description= "Get the hourly energy profile for today to understand usage patterns"
                }),
            CopilotTool.DefineTool(([Description("Appliance name, e.g. dishwasher, washing machine, dryer, EV charger")] string appliance)
                    => energyPlugin.GetApplianceAdviceAsync(appliance),toolOptions, new AIFunctionFactoryOptions()
                    {
                        Name = "get_appliance_advice",
                        Description= "Get advice on the best time to run a high-power appliance based on current and historical production data"
                    }),
            CopilotTool.DefineTool(()=> weatherPlugin.GetCurrentWeatherAsync(),toolOptions, new AIFunctionFactoryOptions()
                    {
                        Name = "get_current_weather",
                        Description= "Get current weather including temperature, cloud cover, and estimated solar irradiance"
                    }),
            CopilotTool.DefineTool(()=> weatherPlugin.GetSolarForecastAsync(),toolOptions, new AIFunctionFactoryOptions()
                    {
                        Name = "get_solar_forecast",
                        Description= "Get the solar production forecast for the next 24 hours based on weather data" 
                    })
        }.AsReadOnly();
    }
}
