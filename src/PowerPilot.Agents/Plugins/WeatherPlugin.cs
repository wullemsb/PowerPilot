using PowerPilot.Core.Interfaces;

namespace PowerPilot.Agents.Plugins;

public class WeatherPlugin
{
    private readonly IWeatherService _weatherService;
    public WeatherPlugin(IWeatherService weatherService) { _weatherService = weatherService; }

    public async Task<string> GetCurrentWeatherAsync(string? location = null)
    {
        var weather = await _weatherService.GetCurrentWeatherAsync(location: location);
        if (weather == null) return "Weather data not available.";
        return $"Current weather in {weather.City}: {weather.Description}, " +
               $"Temperature: {weather.TemperatureCelsius:F1}°C, Cloud cover: {weather.CloudCoverPercent:F0}%, " +
               $"Wind: {weather.WindSpeedMs:F1} m/s, Solar irradiance estimate: {weather.SolarIrradianceEstimate:F0} W/m²";
    }

    public async Task<string> GetSolarForecastAsync(string? location = null)
    {
        var forecast = (await _weatherService.GetForecastAsync(hours: 24, location: location)).ToList();
        if (!forecast.Any()) return "Forecast data not available.";
        var profile = string.Join(", ", forecast.Select(f =>
            $"{f.Timestamp.ToLocalTime().Hour:D2}h: {f.SolarIrradianceEstimate:F0}W/m² ({f.CloudCoverPercent:F0}% clouds)"));
        var peakSolar = forecast.MaxBy(f => f.SolarIrradianceEstimate);
        return $"Solar forecast - Peak at {peakSolar?.Timestamp.ToLocalTime().Hour:D2}h ({peakSolar?.SolarIrradianceEstimate:F0} W/m²). Profile: {profile}";
    }
}
