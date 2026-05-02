namespace PowerPilot.Core.Interfaces;

using PowerPilot.Core.Models;

public interface IWeatherService
{
    Task<WeatherData?> GetCurrentWeatherAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<WeatherData>> GetForecastAsync(int hours = 24, CancellationToken cancellationToken = default);
}
