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

                You are a multi-agent energy monitoring system. Analyze this situation by taking on three specialized roles:

                1. ENERGY ANALYZER ROLE: Evaluate the current energy surplus
                   - Use get_current_power and get_today_stats
                   - Assess magnitude, duration, and tariff implications

                2. APPLIANCE ADVISOR ROLE: Recommend specific appliances
                   - Use get_appliance_advice and get_hourly_profile
                   - Suggest 1-2 high-energy appliances (EV charger 7.4kW, dryer 3kW, dishwasher 1.8kW, etc.)
                   - Consider historical patterns

                3. TIMING OPTIMIZER ROLE: Determine urgency
                   - Use get_current_weather and get_solar_forecast if available
                   - Provide specific timing guidance

                Synthesize your analysis into a single, clear, actionable notification (2-3 sentences max).

                Format your response as JSON:
                {
                    "message": "Your synthesized notification message here",
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
                Tools = tools.ToList(),
                SystemMessage = new SystemMessageConfig
                {
                    Mode = SystemMessageMode.Append,
                    Content = """
                        You are the PowerPilot Energy Monitoring Agent - a multi-role AI assistant specialized in home energy optimization.

                        You can operate in multiple specialized roles to provide comprehensive analysis:

                        **Energy Analyzer**: Evaluates production/consumption patterns, identifies surplus magnitude and duration
                        **Appliance Advisor**: Recommends specific high-energy appliances based on availability and patterns
                        **Timing Optimizer**: Determines urgency and optimal timing based on weather and forecasts

                        When analyzing energy surplus situations:
                        1. Always call relevant tools to gather current data
                        2. Think through each specialized role's perspective
                        3. Synthesize insights into clear, actionable notifications
                        4. Be specific with appliance names and timing guidance
                        5. Keep messages concise (2-3 sentences maximum)

                        Your goal is to help homeowners maximize their solar energy usage by providing timely, actionable recommendations.
                        """
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
