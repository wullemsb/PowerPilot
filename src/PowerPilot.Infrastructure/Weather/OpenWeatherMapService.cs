using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PowerPilot.Core.Interfaces;
using PowerPilot.Core.Models;

namespace PowerPilot.Infrastructure.Weather;

public class OpenWeatherMapService : IWeatherService
{
    private readonly HttpClient _httpClient;
    private readonly WeatherOptions _options;
    private readonly ILogger<OpenWeatherMapService> _logger;
    private WeatherData? _cachedWeather;
    private DateTime _lastFetch = DateTime.MinValue;

    public OpenWeatherMapService(HttpClient httpClient, IOptions<WeatherOptions> options, ILogger<OpenWeatherMapService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<WeatherData?> GetCurrentWeatherAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedWeather != null && (DateTime.UtcNow - _lastFetch).TotalMinutes < 15)
            return _cachedWeather;

        if (string.IsNullOrEmpty(_options.ApiKey))
        {
            _logger.LogWarning("OpenWeatherMap API key not configured. Using mock weather data.");
            return GetMockWeather();
        }

        try
        {
            var url = $"https://api.openweathermap.org/data/2.5/weather?q={Uri.EscapeDataString(_options.City)}&appid={_options.ApiKey}&units={_options.Units}";
            var response = await _httpClient.GetStringAsync(url, cancellationToken);
            var data = JsonSerializer.Deserialize<OWMCurrentResponse>(response);
            if (data == null) return null;

            _cachedWeather = new WeatherData
            {
                Timestamp = DateTime.UtcNow,
                Description = data.Weather?.FirstOrDefault()?.Description ?? "Unknown",
                TemperatureCelsius = data.Main?.Temp ?? 0,
                CloudCoverPercent = data.Clouds?.All ?? 0,
                WindSpeedMs = data.Wind?.Speed ?? 0,
                City = _options.City
            };
            _lastFetch = DateTime.UtcNow;
            return _cachedWeather;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch weather data");
            return GetMockWeather();
        }
    }

    public async Task<IEnumerable<WeatherData>> GetForecastAsync(int hours = 24, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_options.ApiKey)) return GetMockForecast(hours);
        try
        {
            var cnt = Math.Min(hours / 3 + 1, 40);
            var url = $"https://api.openweathermap.org/data/2.5/forecast?q={Uri.EscapeDataString(_options.City)}&appid={_options.ApiKey}&units={_options.Units}&cnt={cnt}";
            var response = await _httpClient.GetStringAsync(url, cancellationToken);
            var data = JsonSerializer.Deserialize<OWMForecastResponse>(response);
            if (data?.List == null) return GetMockForecast(hours);
            return data.List.Select(item => new WeatherData
            {
                Timestamp = DateTimeOffset.FromUnixTimeSeconds(item.Dt).UtcDateTime,
                Description = item.Weather?.FirstOrDefault()?.Description ?? "Unknown",
                TemperatureCelsius = item.Main?.Temp ?? 0,
                CloudCoverPercent = item.Clouds?.All ?? 0,
                WindSpeedMs = item.Wind?.Speed ?? 0,
                City = _options.City
            }).Take(hours / 3);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch forecast data");
            return GetMockForecast(hours);
        }
    }

    private static WeatherData GetMockWeather() => new()
    {
        Timestamp = DateTime.UtcNow, Description = "Partly cloudy",
        TemperatureCelsius = 15, CloudCoverPercent = 40, WindSpeedMs = 3.5, City = "Mock City"
    };

    private static IEnumerable<WeatherData> GetMockForecast(int hours)
    {
        var rng = new Random();
        return Enumerable.Range(0, hours / 3).Select(i => new WeatherData
        {
            Timestamp = DateTime.UtcNow.AddHours(i * 3),
            Description = i % 3 == 0 ? "Sunny" : "Partly cloudy",
            TemperatureCelsius = 15 + rng.NextDouble() * 5,
            CloudCoverPercent = 20 + rng.NextDouble() * 60,
            WindSpeedMs = 2 + rng.NextDouble() * 5,
            City = "Mock City"
        });
    }

    private sealed class OWMCurrentResponse
    {
        [JsonPropertyName("main")] public OWMMain? Main { get; set; }
        [JsonPropertyName("weather")] public List<OWMWeatherItem>? Weather { get; set; }
        [JsonPropertyName("clouds")] public OWMClouds? Clouds { get; set; }
        [JsonPropertyName("wind")] public OWMWind? Wind { get; set; }
    }
    private sealed class OWMForecastResponse { [JsonPropertyName("list")] public List<OWMForecastItem>? List { get; set; } }
    private sealed class OWMForecastItem
    {
        [JsonPropertyName("dt")] public long Dt { get; set; }
        [JsonPropertyName("main")] public OWMMain? Main { get; set; }
        [JsonPropertyName("weather")] public List<OWMWeatherItem>? Weather { get; set; }
        [JsonPropertyName("clouds")] public OWMClouds? Clouds { get; set; }
        [JsonPropertyName("wind")] public OWMWind? Wind { get; set; }
    }
    private sealed class OWMMain { [JsonPropertyName("temp")] public double Temp { get; set; } }
    private sealed class OWMWeatherItem { [JsonPropertyName("description")] public string? Description { get; set; } }
    private sealed class OWMClouds { [JsonPropertyName("all")] public double All { get; set; } }
    private sealed class OWMWind { [JsonPropertyName("speed")] public double Speed { get; set; } }
}
