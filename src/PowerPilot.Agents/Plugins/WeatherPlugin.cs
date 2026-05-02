using System.ComponentModel;
using Microsoft.SemanticKernel;
using PowerPilot.Core.Interfaces;

namespace PowerPilot.Agents.Plugins;

public class WeatherPlugin
{
    private readonly IWeatherService _weatherService;
    public WeatherPlugin(IWeatherService weatherService) { _weatherService = weatherService; }

    [KernelFunction("get_current_weather")]
    [Description("Get the current weather conditions including temperature, cloud cover and wind speed")]
    public async Task<string> GetCurrentWeatherAsync()
    {
        var weather = await _weatherService.GetCurrentWeatherAsync();
        if (weather == null) return "Weather data not available.";
        return $"Current weather in {weather.City}: {weather.Description}, " +
               $"Temperature: {weather.TemperatureCelsius:F1}°C, Cloud cover: {weather.CloudCoverPercent:F0}%, " +
               $"Wind: {weather.WindSpeedMs:F1} m/s, Solar irradiance estimate: {weather.SolarIrradianceEstimate:F0} W/m²";
    }

    [KernelFunction("get_solar_forecast")]
    [Description("Get the solar production forecast based on weather forecast for the next 24 hours")]
    public async Task<string> GetSolarForecastAsync()
    {
        var forecast = (await _weatherService.GetForecastAsync(24)).ToList();
        if (!forecast.Any()) return "Forecast data not available.";
        var profile = string.Join(", ", forecast.Select(f =>
            $"{f.Timestamp.ToLocalTime().Hour:D2}h: {f.SolarIrradianceEstimate:F0}W/m² ({f.CloudCoverPercent:F0}% clouds)"));
        var peakSolar = forecast.MaxBy(f => f.SolarIrradianceEstimate);
        return $"Solar forecast - Peak at {peakSolar?.Timestamp.ToLocalTime().Hour:D2}h ({peakSolar?.SolarIrradianceEstimate:F0} W/m²). Profile: {profile}";
    }
}
