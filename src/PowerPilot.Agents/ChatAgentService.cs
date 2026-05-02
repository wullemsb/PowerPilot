#pragma warning disable SKEXP0001
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace PowerPilot.Agents;

public class AgentOptions
{
    public string? GitHubToken { get; set; }
    public string ModelId { get; set; } = "gpt-4o-mini";
}

public class ChatAgentService
{
    private readonly ILogger<ChatAgentService> _logger;
    private readonly Kernel _kernel;
    private IChatCompletionService? _chatCompletion;
    private readonly ChatHistory _chatHistory = new();
    private readonly bool _hasLlm;

    public ChatAgentService(ILogger<ChatAgentService> logger, Kernel kernel)
    {
        _logger = logger;
        _kernel = kernel;

        _hasLlm = kernel.Services.GetService(typeof(IChatCompletionService)) != null;
        if (_hasLlm)
            _chatCompletion = kernel.GetRequiredService<IChatCompletionService>();

        _chatHistory.AddSystemMessage(
            """
            You are PowerPilot, an intelligent home energy management assistant. 
            You help homeowners understand their energy consumption, solar production, and optimize their energy usage.
            You have access to real-time P1 smart meter data, historical energy consumption data, and weather information.
            Always use the available tools/functions to get real-time data before answering.
            Provide clear, actionable advice. Use kW for power and kWh for energy.
            """);
    }

    public async IAsyncEnumerable<string> ChatAsync(string userMessage, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!_hasLlm || _chatCompletion == null)
        {
            yield return "AI chat is not configured. Please set a GitHubToken in appsettings.json under the 'Agent' section. The energy plugins are still available.";
            yield break;
        }

        _chatHistory.AddUserMessage(userMessage);
        _logger.LogDebug("Processing chat message: {Message}", userMessage);

        var executionSettings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            MaxTokens = 1000
        };

        var response = new System.Text.StringBuilder();

        await foreach (var chunk in _chatCompletion.GetStreamingChatMessageContentsAsync(
            _chatHistory, executionSettings, _kernel, cancellationToken))
        {
            if (!string.IsNullOrEmpty(chunk.Content))
            {
                response.Append(chunk.Content);
                yield return chunk.Content;
            }
        }

        if (response.Length > 0)
            _chatHistory.AddAssistantMessage(response.ToString());
    }

    public void ClearHistory()
    {
        var systemMessage = _chatHistory.FirstOrDefault(m => m.Role == AuthorRole.System);
        _chatHistory.Clear();
        if (systemMessage != null) _chatHistory.Add(systemMessage);
    }
}
#pragma warning restore SKEXP0001
