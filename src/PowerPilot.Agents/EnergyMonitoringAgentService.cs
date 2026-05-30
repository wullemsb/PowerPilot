using GitHub.Copilot;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PowerPilot.Agents.Plugins;
using PowerPilot.Core.Interfaces;
using PowerPilot.Core.Models;

namespace PowerPilot.Agents;

/// <summary>
/// Background service that monitors energy production and consumption.
/// Uses a multi-agent AI system to generate intelligent notifications when significant
/// unused energy is available.
/// </summary>
public class EnergyMonitoringAgentService : BackgroundService
{
    private static readonly IReadOnlyList<MonitoringSubAgentDefinition> AvailableSubAgents =
    [
        new MonitoringSubAgentDefinition(
            "energy_analyzer",
            "Energy Analyzer",
            "Evaluates production/consumption patterns, identifies surplus magnitude and duration, assesses tariff implications.",
            """
            You are the Energy Analyzer agent. Your role is to evaluate the current energy situation.

            When asked to analyze energy surplus:
            1. Call get_current_power to understand current net production
            2. Call get_today_stats to see daily context
            3. Call get_hourly_profile to identify patterns
            4. Assess the magnitude and significance of the surplus
            5. Consider tariff implications (day vs night rates)

            Provide a brief analysis focusing on:
            - How much surplus energy is available
            - How significant this surplus is compared to typical patterns
            - How long this surplus might last based on hourly patterns

            Keep your response concise and data-driven.
            """,
            ["get_current_power", "get_today_stats", "get_energy_stats", "get_hourly_profile"]),
        new MonitoringSubAgentDefinition(
            "appliance_advisor",
            "Appliance Advisor",
            "Recommends specific high-energy appliances to run based on available surplus and historical patterns.",
            """
            You are the Appliance Advisor agent. Your role is to recommend which appliances to run.

            High-power appliances and their typical consumption:
            - EV Charger: 7.4 kW
            - Dryer: 3.0 kW
            - Washing Machine: 2.0 kW
            - Dishwasher: 1.8 kW

            When asked for recommendations:
            1. Call get_current_power to see available surplus
            2. Call get_appliance_advice for 1-2 relevant appliances
            3. Consider which appliances fit the available surplus
            4. Prioritize appliances that match the surplus magnitude

            Provide specific recommendations:
            - Which 1-2 appliances should be run NOW
            - Why those appliances are good matches for the current surplus

            Be specific with appliance names. Keep response brief.
            """,
            ["get_current_power", "get_appliance_advice", "get_hourly_profile"]),
        new MonitoringSubAgentDefinition(
            "timing_optimizer",
            "Timing Optimizer",
            "Determines urgency and optimal timing windows based on weather forecasts and solar production predictions.",
            """
            You are the Timing Optimizer agent. Your role is to determine timing urgency.

            When asked about timing:
            1. Call get_current_weather to assess current solar conditions
            2. Call get_solar_forecast to predict upcoming production
            3. Determine how long the surplus will likely last
            4. Assess urgency based on cloud cover and forecast trends

            Provide timing guidance:
            - How urgent is it to act NOW vs later
            - How long the surplus window is likely to last
            - Whether conditions will improve or worsen soon

            Be specific with timing windows (e.g., "next 2 hours", "before 3pm").
            """,
            ["get_current_weather", "get_solar_forecast", "get_hourly_profile"])
    ];

    private readonly ILogger<EnergyMonitoringAgentService> _logger;
    private readonly IEnergyStateService _energyState;
    private readonly INotificationService _notificationService;
    private readonly CopilotClient _copilotClient;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EnergyMonitoringOptions _options;
    private readonly string _model;
    private readonly EnergyMonitoringStateEvaluator _stateEvaluator;
    private DateTime? _lastNotificationTime;
    private DateTime? _thresholdExceededSince;

    public EnergyMonitoringAgentService(
        ILogger<EnergyMonitoringAgentService> logger,
        IEnergyStateService energyState,
        INotificationService notificationService,
        CopilotClient copilotClient,
        IServiceScopeFactory scopeFactory,
        IOptions<EnergyMonitoringOptions> options,
        IOptions<AgentOptions> agentOptions)
    {
        _logger = logger;
        _energyState = energyState;
        _notificationService = notificationService;
        _copilotClient = copilotClient;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _model = agentOptions.Value.Model;
        _stateEvaluator = new EnergyMonitoringStateEvaluator(_options);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Energy Monitoring Agent started. Interval: {Interval}s, Threshold: {Threshold} kW, Duration: {Duration} min, Cooldown: {Cooldown} min",
            _options.MonitoringIntervalSeconds,
            _options.EnergyThresholdKw,
            _options.ThresholdDurationMinutes,
            _options.NotificationCooldownMinutes);

        // Wait a bit for the system to initialize
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await MonitorEnergyAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during energy monitoring cycle");
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.MonitoringIntervalSeconds), stoppingToken);
        }
    }

    private async Task MonitorEnergyAsync(CancellationToken cancellationToken)
    {
        var telegram = _energyState.CurrentTelegram;
        var previousThresholdExceededSince = _thresholdExceededSince;
        var now = DateTime.UtcNow;
        var evaluation = _stateEvaluator.Evaluate(telegram, now, _thresholdExceededSince, _lastNotificationTime);
        _thresholdExceededSince = evaluation.ThresholdExceededSince;

        if (!evaluation.HasTelegram)
        {
            _logger.LogDebug("No current telegram data available");
            return;
        }

        if (!evaluation.ExceedsThreshold)
        {
            if (previousThresholdExceededSince != null)
            {
                _logger.LogDebug("Energy surplus dropped below threshold. Resetting timer.");
            }

            return;
        }

        if (previousThresholdExceededSince == null && _thresholdExceededSince != null)
        {
            _logger.LogDebug(
                "Energy threshold exceeded: {NetPower} kW (threshold: {Threshold} kW). Starting timer.",
                telegram!.NetPower,
                _options.EnergyThresholdKw);
            return;
        }

        if (!evaluation.RequiredDurationMet)
        {
            _logger.LogDebug(
                "Threshold exceeded for {Current} min, need {Required} min",
                evaluation.ExceededDuration.TotalMinutes,
                evaluation.RequiredDuration.TotalMinutes);
            return;
        }

        if (!evaluation.CooldownElapsed)
        {
            _logger.LogDebug(
                "Cooldown active. Next notification possible in {Minutes} minutes.",
                evaluation.CooldownRemaining.TotalMinutes);
            return;
        }

        _logger.LogInformation(
            "Sustained energy surplus detected: {NetPower} kW for {Duration} minutes. Generating notification.",
            telegram!.NetPower,
            evaluation.ExceededDuration.TotalMinutes);

        await GenerateAndSendNotificationAsync(telegram.NetPower, cancellationToken);
        _lastNotificationTime = now;
    }

    private async Task GenerateAndSendNotificationAsync(decimal energySurplusKw, CancellationToken cancellationToken)
    {
        try
        {
            var enabledSubAgents = GetEnabledSubAgentDefinitions();
            await using var sessionContext = await CreateSessionContextAsync(enabledSubAgents, cancellationToken);
            var session = sessionContext.Session;
            var prompt = $$"""
                URGENT ENERGY SURPLUS DETECTED

                Current situation:
                - Net power production: {{energySurplusKw:F2}} kW
                - This surplus has been sustained for {{_options.ThresholdDurationMinutes}} minutes

                {{BuildPromptInstructions(enabledSubAgents)}}

                Return your response as JSON:
                {
                    "message": "Your synthesized notification message (2-3 sentences max)",
                    "severity": "info|success|warning|important"
                }
                """;

            var agentContributions = new Dictionary<string, string>();
            var responseBuilder = new System.Text.StringBuilder();

            using var subscription = session.On<SessionEvent>(evt =>
            {
                if (evt is AssistantMessageDeltaEvent delta && !string.IsNullOrEmpty(delta.Data?.DeltaContent))
                {
                    responseBuilder.Append(delta.Data.DeltaContent);
                }
            });

            await session.SendAndWaitAsync(new MessageOptions { Prompt = prompt });
            var response = responseBuilder.ToString();

            _logger.LogDebug("Agent response: {Response}", response);

            // Try to parse JSON response
            var notification = ParseNotificationResponse(response, energySurplusKw);

            // For now, we'll capture the full response as a single contribution
            // In a future enhancement, we could parse individual agent responses
            agentContributions["orchestrator"] = response;
            notification.AgentContributions = agentContributions;

            await _notificationService.AddNotificationAsync(notification);

            _logger.LogInformation(
                "Notification generated and sent: {Message}",
                notification.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate notification using AI agents");

            // Fallback: send a simple notification without AI
            await _notificationService.AddNotificationAsync(new EnergyNotification
            {
                Message = $"High solar production detected: {energySurplusKw:F2} kW surplus available. Consider running high-power appliances.",
                EnergySurplusKw = energySurplusKw,
                Severity = NotificationSeverity.Success
            });
        }
    }

    private EnergyNotification ParseNotificationResponse(string response, decimal energySurplusKw)
    {
        try
        {
            // Try to extract JSON from the response
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonString = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var json = System.Text.Json.JsonDocument.Parse(jsonString);

                var message = json.RootElement.GetProperty("message").GetString() ?? string.Empty;
                var severityStr = json.RootElement.TryGetProperty("severity", out var sevProp) 
                    ? sevProp.GetString() 
                    : "info";

                var severity = severityStr?.ToLower() switch
                {
                    "success" => NotificationSeverity.Success,
                    "warning" => NotificationSeverity.Warning,
                    "important" => NotificationSeverity.Important,
                    _ => NotificationSeverity.Info
                };

                return new EnergyNotification
                {
                    Message = message,
                    EnergySurplusKw = energySurplusKw,
                    Severity = severity
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse JSON from agent response, using response as-is");
        }

        // Fallback: use the response text directly
        return new EnergyNotification
        {
            Message = response.Length > 300 ? response.Substring(0, 300) + "..." : response,
            EnergySurplusKw = energySurplusKw,
            Severity = NotificationSeverity.Success
        };
    }

    private async Task<MonitoringSessionContext> CreateSessionContextAsync(
        IReadOnlyList<MonitoringSubAgentDefinition> enabledSubAgents,
        CancellationToken cancellationToken)
    {
        var scope = _scopeFactory.CreateAsyncScope();
        try
        {
            _logger.LogInformation("Creating Copilot session for energy monitoring agent (model: {Model})", _model);

            var energyPlugin = scope.ServiceProvider.GetRequiredService<EnergyPlugin>();
            var weatherPlugin = scope.ServiceProvider.GetRequiredService<WeatherPlugin>();
            var tools = PowerPilotAgentFactory.BuildTools(energyPlugin, weatherPlugin);
            var session = await _copilotClient.CreateSessionAsync(new SessionConfig
            {
                Model = _model,
                Streaming = true,
                OnPermissionRequest = PermissionHandler.ApproveAll,
                ExcludedTools = enabledSubAgents.Count > 0 ? tools.Select(t => t.Name).ToList() : null,
                Tools = tools.ToList(),
                Agent = "monitor",
                CustomAgents = BuildCustomAgents(enabledSubAgents)
            }, cancellationToken);

            return new MonitoringSessionContext(scope, session);
        }
        catch
        {
            await scope.DisposeAsync();
            throw;
        }
    }

    private List<CustomAgentConfig> BuildCustomAgents(IReadOnlyList<MonitoringSubAgentDefinition> enabledSubAgents)
    {
        var monitorPrompt = BuildMonitorPrompt(enabledSubAgents);
        var customAgents = new List<CustomAgentConfig>
        {
            new()
            {
                Name = "monitor",
                DisplayName = "Energy Monitor Orchestrator",
                Description = "Orchestrates multi-agent analysis to provide actionable energy insights.",
                Prompt = monitorPrompt
            }
        };

        customAgents.AddRange(enabledSubAgents.Select(definition => new CustomAgentConfig
        {
            Name = definition.Name,
            DisplayName = definition.DisplayName,
            Description = definition.Description,
            Tools = definition.Tools.ToList(),
            Prompt = definition.Prompt
        }));

        return customAgents;
    }

    private IReadOnlyList<MonitoringSubAgentDefinition> GetEnabledSubAgentDefinitions()
    {
        foreach (var configuredAgent in _options.EnabledSubAgents.Keys)
        {
            if (!AvailableSubAgents.Any(agent => string.Equals(agent.Name, configuredAgent, StringComparison.Ordinal)))
            {
                _logger.LogWarning("Unknown monitoring sub-agent '{AgentName}' configured in EnergyMonitoring:EnabledSubAgents", configuredAgent);
            }
        }

        return AvailableSubAgents
            .Where(agent => !_options.EnabledSubAgents.TryGetValue(agent.Name, out var enabled) || enabled)
            .ToList();
    }

    private static string BuildPromptInstructions(IReadOnlyList<MonitoringSubAgentDefinition> enabledSubAgents)
    {
        if (enabledSubAgents.Count == 0)
        {
            return "Analyze the situation directly using the available tools, then synthesize ONE clear notification message.";
        }

        var instructions = enabledSubAgents
            .Select((agent, index) => $"{index + 1}. Ask the {agent.Name} agent to help analyze this situation")
            .ToList();

        instructions.Add($"{instructions.Count + 1}. Then synthesize their insights into ONE clear notification message.");
        return "Please coordinate with your specialized agents to analyze this situation:\n\n" + string.Join("\n", instructions);
    }

    private static string BuildMonitorPrompt(IReadOnlyList<MonitoringSubAgentDefinition> enabledSubAgents)
    {
        if (enabledSubAgents.Count == 0)
        {
            return """
                You are the PowerPilot Energy Monitor. Analyze energy surplus situations directly using the available tools.

                When you receive an energy surplus alert:
                1. Review the current power data and recent trends.
                2. Determine whether action is urgent.
                3. Recommend a concise, actionable next step.
                4. Return ONLY a JSON response in this format:
                {
                    "message": "Your synthesized notification here",
                    "severity": "info|success|warning|important"
                }

                Keep the final message actionable, specific, and concise.
                """;
        }

        var availableAgents = string.Join(
            Environment.NewLine,
            enabledSubAgents.Select(agent => $"- **{agent.Name}**: {agent.Description}"));
        var steps = string.Join(
            Environment.NewLine,
            enabledSubAgents.Select((agent, index) => $"{index + 1}. Delegate to {agent.Name}"));

        return $$"""
            You are the PowerPilot Energy Monitor Orchestrator. You coordinate specialized agents to analyze energy situations.

            Available specialized agents:
            {{availableAgents}}

            When you receive an energy surplus alert:
            {{steps}}
            {{enabledSubAgents.Count + 1}}. Synthesize their responses into ONE concise notification (2-3 sentences max)
            {{enabledSubAgents.Count + 2}}. Return ONLY a JSON response in this format:
            {
                "message": "Your synthesized notification here",
                "severity": "info|success|warning|important"
            }

            Keep the final message actionable, specific, and concise.
            """;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Energy Monitoring Agent stopping");
        return base.StopAsync(cancellationToken);
    }

    private sealed record MonitoringSubAgentDefinition(
        string Name,
        string DisplayName,
        string Description,
        string Prompt,
        IReadOnlyList<string> Tools);

    private sealed class MonitoringSessionContext : IAsyncDisposable
    {
        private readonly AsyncServiceScope _scope;

        public MonitoringSessionContext(AsyncServiceScope scope, CopilotSession session)
        {
            _scope = scope;
            Session = session;
        }

        public CopilotSession Session { get; }

        public async ValueTask DisposeAsync()
        {
            await Session.DisposeAsync();
            await _scope.DisposeAsync();
        }
    }
}
