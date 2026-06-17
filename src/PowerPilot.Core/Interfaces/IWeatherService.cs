namespace PowerPilot.Core.Interfaces;

using PowerPilot.Core.Models;

public interface IWeatherService
{
    Task<WeatherData?> GetCurrentWeatherAsync(string? location = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<WeatherData>> GetForecastAsync(int hours = 24, string? location = null, CancellationToken cancellationToken = default);
}
