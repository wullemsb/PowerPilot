using GitHub.Copilot.SDK;
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
    private readonly ILogger<EnergyMonitoringAgentService> _logger;
    private readonly IEnergyStateService _energyState;
    private readonly INotificationService _notificationService;
    private readonly CopilotClient _copilotClient;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EnergyMonitoringOptions _options;

    private CopilotSession? _session;
    private DateTime? _lastNotificationTime;
    private DateTime? _thresholdExceededSince;
    private readonly SemaphoreSlim _sessionLock = new(1, 1);

    public EnergyMonitoringAgentService(
        ILogger<EnergyMonitoringAgentService> logger,
        IEnergyStateService energyState,
        INotificationService notificationService,
        CopilotClient copilotClient,
        IServiceScopeFactory scopeFactory,
        IOptions<EnergyMonitoringOptions> options)
    {
        _logger = logger;
        _energyState = energyState;
        _notificationService = notificationService;
        _copilotClient = copilotClient;
        _scopeFactory = scopeFactory;
        _options = options.Value;
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
        if (telegram == null)
        {
            _logger.LogDebug("No current telegram data available");
            return;
        }

        var netPower = telegram.NetPower;
        var isProducing = telegram.IsProducing;
        var exceedsThreshold = isProducing && netPower >= _options.EnergyThresholdKw;

        if (exceedsThreshold)
        {
            // Track how long the threshold has been exceeded
            if (_thresholdExceededSince == null)
            {
                _thresholdExceededSince = DateTime.UtcNow;
                _logger.LogDebug(
                    "Energy threshold exceeded: {NetPower} kW (threshold: {Threshold} kW). Starting timer.",
                    netPower,
                    _options.EnergyThresholdKw);
            }
            else
            {
                var exceededDuration = DateTime.UtcNow - _thresholdExceededSince.Value;
                var requiredDuration = TimeSpan.FromMinutes(_options.ThresholdDurationMinutes);

                if (exceededDuration >= requiredDuration)
                {
                    // Check cooldown period
                    var cooldownPeriod = TimeSpan.FromMinutes(_options.NotificationCooldownMinutes);
                    var timeSinceLastNotification = _lastNotificationTime.HasValue
                        ? DateTime.UtcNow - _lastNotificationTime.Value
                        : TimeSpan.MaxValue;

                    if (timeSinceLastNotification >= cooldownPeriod)
                    {
                        _logger.LogInformation(
                            "Sustained energy surplus detected: {NetPower} kW for {Duration} minutes. Generating notification.",
                            netPower,
                            exceededDuration.TotalMinutes);

                        await GenerateAndSendNotificationAsync(netPower, cancellationToken);
                        _lastNotificationTime = DateTime.UtcNow;
                        _thresholdExceededSince = null; // Reset the timer
                    }
                    else
                    {
                        _logger.LogDebug(
                            "Cooldown active. Next notification possible in {Minutes} minutes.",
                            (cooldownPeriod - timeSinceLastNotification).TotalMinutes);
                    }
                }
                else
                {
                    _logger.LogDebug(
                        "Threshold exceeded for {Current} min, need {Required} min",
                        exceededDuration.TotalMinutes,
                        requiredDuration.TotalMinutes);
                }
            }
        }
        else
        {
            // Reset if threshold is no longer exceeded
            if (_thresholdExceededSince != null)
            {
                _logger.LogDebug("Energy surplus dropped below threshold. Resetting timer.");
                _thresholdExceededSince = null;
            }
        }
    }

    private async Task GenerateAndSendNotificationAsync(decimal energySurplusKw, CancellationToken cancellationToken)
    {
        try
        {
            var session = await GetOrCreateSessionAsync(cancellationToken);
            if (session == null)
            {
                _logger.LogWarning("Failed to create Copilot session for notification generation");
                return;
            }


            var prompt = $$"""
                URGENT ENERGY SURPLUS DETECTED

                Current situation:
                - Net power production: {{energySurplusKw:F2}} kW
                - This surplus has been sustained for {{_options.ThresholdDurationMinutes}} minutes

                Please coordinate with your specialized agents to analyze this situation:

                1. Ask the energy_analyzer agent to evaluate the current surplus situation
                2. Ask the appliance_advisor agent to recommend 1-2 specific appliances
                3. Ask the timing_optimizer agent to determine urgency and timing

                Then synthesize their insights into ONE clear notification message.

                Return your response as JSON:
                {
                    "message": "Your synthesized notification message (2-3 sentences max)",
                    "severity": "info|success|warning|important"
                }
                """;

            var agentContributions = new Dictionary<string, string>();
            var responseBuilder = new System.Text.StringBuilder();

            session.On(evt =>
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

    private async Task<CopilotSession?> GetOrCreateSessionAsync(CancellationToken cancellationToken)
    {
        await _sessionLock.WaitAsync(cancellationToken);
        try
        {
            if (_session != null)
                return _session;

            _logger.LogInformation("Creating Copilot session for energy monitoring agent");

            // Create scoped services for plugins
            using var scope = _scopeFactory.CreateScope();
            var energyPlugin = scope.ServiceProvider.GetRequiredService<EnergyPlugin>();
            var weatherPlugin = scope.ServiceProvider.GetRequiredService<WeatherPlugin>();

            var tools = PowerPilotAgentFactory.BuildTools(energyPlugin, weatherPlugin);

            _session = await _copilotClient.CreateSessionAsync(new SessionConfig
            {
                Model = "gpt-4.1",
                Streaming = true,
                OnPermissionRequest = PermissionHandler.ApproveAll,
                ExcludedTools= tools.Select(t=> t.Name).ToList(), // Exclude all tools from the main agent - they will be used by specialized agents
                Tools= tools.ToList(),
                Agent = "monitor", // Start with the orchestrator agent
                CustomAgents = new List<CustomAgentConfig>
                {
                    new()
                    {
                        Name = "monitor",
                        DisplayName = "Energy Monitor Orchestrator",
                        Description = "Orchestrates multi-agent analysis to provide actionable energy insights. Delegates to specialized agents.",
                        Prompt = """
                        You are the PowerPilot Energy Monitor Orchestrator. You coordinate specialized agents to analyze energy situations.

                        Available specialized agents:
                        - **energy_analyzer**: Call this agent to evaluate current energy surplus, production/consumption patterns
                        - **appliance_advisor**: Call this agent to get appliance recommendations based on current surplus
                        - **timing_optimizer**: Call this agent to determine urgency and optimal timing windows

                        When you receive an energy surplus alert:
                        1. Delegate to energy_analyzer to assess the situation
                        2. Delegate to appliance_advisor to get specific recommendations
                        3. Delegate to timing_optimizer to determine urgency
                        4. Synthesize their responses into ONE concise notification (2-3 sentences max)
                        5. Return ONLY a JSON response in this format:
                        {
                            "message": "Your synthesized notification here",
                            "severity": "info|success|warning|important"
                        }

                        Keep the final message actionable, specific, and concise.
                        """
                    },
                    new()
                    {
                        Name = "energy_analyzer",
                        DisplayName = "Energy Analyzer",
                        Description = "Evaluates production/consumption patterns, identifies surplus magnitude and duration, assesses tariff implications.",
                        Tools = new List<string> 
                        { 
                            "get_current_power", 
                            "get_today_stats",
                            "get_energy_stats",
                            "get_hourly_profile"
                        },
                        Prompt = """
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
                        """
                    },
                    new()
                    {
                        Name = "appliance_advisor",
                        DisplayName = "Appliance Advisor",
                        Description = "Recommends specific high-energy appliances to run based on available surplus and historical patterns.",
                        Tools = new List<string> 
                        { 
                            "get_current_power",
                            "get_appliance_advice",
                            "get_hourly_profile"
                        },
                        Prompt = """
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
                        """
                    },
                    new()
                    {
                        Name = "timing_optimizer",
                        DisplayName = "Timing Optimizer",
                        Description = "Determines urgency and optimal timing windows based on weather forecasts and solar production predictions.",
                        Tools = new List<string> 
                        { 
                            "get_current_weather",
                            "get_solar_forecast",
                            "get_hourly_profile"
                        },
                        Prompt = """
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
                        """
                    }
                }
            }, cancellationToken);

            return _session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Copilot session");
            return null;
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Energy Monitoring Agent stopping");

        if (_session != null)
        {
            await _session.DisposeAsync();
            _session = null;
        }

        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _sessionLock.Dispose();
        base.Dispose();
    }
}
