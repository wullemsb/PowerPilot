using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using PowerPilot.Agents.Plugins;

namespace PowerPilot.Agents;

public static class PowerPilotKernelFactory
{
    public static Kernel CreateKernel(IServiceProvider services, string? githubToken = null, string? modelId = null)
    {
        var builder = Kernel.CreateBuilder();

        if (!string.IsNullOrEmpty(githubToken))
        {
            builder.AddAzureOpenAIChatCompletion(
                deploymentName: modelId ?? "gpt-4o-mini",
                endpoint: "https://models.inference.ai.azure.com",
                apiKey: githubToken);
        }

        builder.Services.AddLogging(l => l.AddConsole());
        var kernel = builder.Build();

        var energyPlugin = ActivatorUtilities.CreateInstance<EnergyPlugin>(services);
        var weatherPlugin = ActivatorUtilities.CreateInstance<WeatherPlugin>(services);
        kernel.Plugins.AddFromObject(energyPlugin, "EnergyPlugin");
        kernel.Plugins.AddFromObject(weatherPlugin, "WeatherPlugin");

        return kernel;
    }
}
