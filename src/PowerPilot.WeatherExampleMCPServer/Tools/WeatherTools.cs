using ModelContextProtocol.Server;
using PowerPilot.Core.Interfaces;
using PowerPilot.Core.Models;
using System.ComponentModel;

/// <summary>
/// MCP tools that expose weather data.
/// </summary>
internal class WeatherTools
{
    private readonly IWeatherService _weatherService;

    public WeatherTools(IWeatherService weatherService)
    {
        _weatherService = weatherService;
    }

    [McpServerTool]
    [Description("Gets the current weather data.")]
    public Task<WeatherData?> GetCurrentWeatherAsync(
        [Description("Optional location, e.g. Brussels or Amsterdam.")] string? location = null,
        CancellationToken cancellationToken = default)
    {
        return _weatherService.GetCurrentWeatherAsync(location: location, cancellationToken: cancellationToken);
    }

    [McpServerTool]
    [Description("Gets weather forecast data for the next N hours.")]
    public async Task<IEnumerable<WeatherData>> GetForecastAsync(
        [Description("Number of hours to include in the forecast.")] int hours = 24,
        [Description("Optional location, e.g. Brussels or Amsterdam.")] string? location = null,
        CancellationToken cancellationToken = default)
    {
        return await _weatherService.GetForecastAsync(hours: hours, location: location, cancellationToken: cancellationToken);
    }
}
