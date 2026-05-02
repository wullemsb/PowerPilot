namespace PowerPilot.Core.Interfaces;

using PowerPilot.Core.Models;

public interface IEnergyRepository
{
    Task SaveReadingAsync(EnergyReading reading, CancellationToken cancellationToken = default);
    Task<EnergyReading?> GetLatestReadingAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<EnergyReading>> GetReadingsAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<EnergyStats> GetStatsAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<IEnumerable<EnergyReading>> GetHourlyAveragesAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
}
