using System.Threading.Channels;
using GitHub.Copilot;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PowerPilot.Agents.Plugins;

namespace PowerPilot.Agents;

public class AgentOptions
{
    /// <summary>GitHub personal access token with Copilot access.</summary>
    public string? GitHubToken { get; set; }

    /// <summary>
    /// Path to the GitHub Copilot CLI binary.
    /// Defaults to the bundled binary or the <c>COPILOT_CLI_PATH</c> environment variable.
    /// </summary>
    public string? CliPath { get; set; }

    public string? CliUrl { get; set; }

    /// <summary>
    /// GitHub Copilot model to use, e.g. "gpt-4.1", "claude-sonnet-4.5".
    /// See <c>CopilotClient.ListModelsAsync()</c> for available models.
    /// </summary>
    public string Model { get; set; } = "gpt-4.1";
}

/// <summary>
/// Scoped service that manages a single GitHub Copilot <see cref="CopilotSession"/>
/// per Blazor circuit and exposes a streaming chat API.
/// </summary>
public sealed class ChatAgentService : IAsyncDisposable
{
    private const string SystemPrompt =
        """
        You are PowerPilot, an intelligent home energy management assistant.
        You help homeowners understand their electricity consumption, solar production, and gas usage,
        and advise them on how to optimise their energy use.

        You have access to real-time P1 smart meter data, historical energy readings, and weather information.
        Always call the relevant tools to fetch up-to-date data before answering.
        Provide clear, concise, and actionable advice.
        Use kW for instantaneous power and kWh for energy totals.
        """;

    private readonly ILogger<ChatAgentService> _logger;
    private readonly CopilotClient _client;
    private readonly IReadOnlyList<AIFunctionDeclaration> _tools;
    private readonly string _model;

    private CopilotSession? _session;
    private readonly SemaphoreSlim _sessionLock = new(1, 1);

    public ChatAgentService(
        ILogger<ChatAgentService> logger,
        CopilotClient client,
        IOptions<AgentOptions> options,
        EnergyPlugin energyPlugin,
        WeatherPlugin weatherPlugin)
    {
        _logger = logger;
        _client = client;
        _model = options.Value.Model;
        _tools = PowerPilotAgentFactory.BuildTools(energyPlugin, weatherPlugin);
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Sends <paramref name="userMessage"/> to GitHub Copilot and streams back
    /// the assistant response token by token.
    /// </summary>
    public async IAsyncEnumerable<string> ChatAsync(
        string userMessage,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        CopilotSession? session = null;
        Exception? sessionError = null;
        try
        {
            session = await GetOrCreateSessionAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create GitHub Copilot session");
            sessionError = ex;
        }

        if (session == null || sessionError != null)
        {
            yield return BuildUnavailableMessage();
            yield break;
        }

        // Use an unbounded channel to bridge the SDK's event callbacks into an
        // async-enumerable so the Blazor component can stream tokens.
        var channel = Channel.CreateUnbounded<string>(
            new UnboundedChannelOptions { SingleWriter = true, SingleReader = true });

        IDisposable? subscription = null;

        subscription = session.On<SessionEvent>(evt =>
        {
            switch (evt)
            {
                case AssistantMessageDeltaEvent delta
                    when !string.IsNullOrEmpty(delta.Data?.DeltaContent):
                    channel.Writer.TryWrite(delta.Data.DeltaContent);
                    break;

                case SessionIdleEvent:
                    channel.Writer.TryComplete();
                    subscription?.Dispose();
                    break;

                case SessionErrorEvent err:
                    var message = err.Data?.Message ?? "Unknown session error";
                    _logger.LogError("Copilot session error: {Message}", message);
                    channel.Writer.TryComplete(new InvalidOperationException(message));
                    subscription?.Dispose();
                    break;
            }
        });

        try
        {
            await session.SendAsync(new MessageOptions { Prompt = userMessage }, cancellationToken);
            await foreach (var chunk in channel.Reader.ReadAllAsync(cancellationToken))
                yield return chunk;
        }
        finally
        {
            subscription?.Dispose();
        }
    }

    /// <summary>
    /// Discards the current session so the next call to <see cref="ChatAsync"/>
    /// starts a fresh conversation.
    /// </summary>
    public void ClearHistory() => _ = ClearHistoryAsync();

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task<CopilotSession> GetOrCreateSessionAsync(CancellationToken ct)
    {
        await _sessionLock.WaitAsync(ct);
        try
        {
            if (_session != null)
                return _session;

            _logger.LogInformation("Creating new GitHub Copilot session (model: {Model})", _model);

            _session = await _client.CreateSessionAsync(new SessionConfig
            {
                Model = _model,
                Streaming = true,
                OnPermissionRequest = PermissionHandler.ApproveAll,
                Tools = _tools.ToList(),
                SystemMessage = new SystemMessageConfig
                {
                    Mode = SystemMessageMode.Append,
                    Content = SystemPrompt,
                },
            }, ct);

            return _session;
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    private async Task ClearHistoryAsync()
    {
        await _sessionLock.WaitAsync();
        try
        {
            if (_session != null)
            {
                await _session.DisposeAsync();
                _session = null;
            }
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    private static string BuildUnavailableMessage() =>
        "GitHub Copilot is not available. Please ensure:\n" +
        "• The GitHub Copilot CLI is installed (npm install -g @github/copilot-cli) or bundled with the app\n" +
        "• You are authenticated (run 'gh auth login') or set Agent:GitHubToken in appsettings.json\n" +
        "• You have an active GitHub Copilot subscription\n\n" +
        "The energy dashboard is still fully functional without AI chat.";

    public async ValueTask DisposeAsync()
    {
        if (_session != null)
            await _session.DisposeAsync();

        _sessionLock.Dispose();
    }
}

