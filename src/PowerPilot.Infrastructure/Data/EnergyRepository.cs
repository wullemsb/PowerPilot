using Microsoft.EntityFrameworkCore;
using PowerPilot.Core.Interfaces;
using PowerPilot.Core.Models;

namespace PowerPilot.Infrastructure.Data;

public class EnergyRepository : IEnergyRepository
{
    private readonly EnergyDbContext _context;
    public EnergyRepository(EnergyDbContext context) { _context = context; }

    public async Task SaveReadingAsync(EnergyReading reading, CancellationToken cancellationToken = default)
    {
        _context.EnergyReadings.Add(reading);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<EnergyReading?> GetLatestReadingAsync(CancellationToken cancellationToken = default)
        => await _context.EnergyReadings.OrderByDescending(r => r.Timestamp).FirstOrDefaultAsync(cancellationToken);

    public async Task<IEnumerable<EnergyReading>> GetReadingsAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
        => await _context.EnergyReadings.Where(r => r.Timestamp >= from && r.Timestamp <= to).OrderBy(r => r.Timestamp).ToListAsync(cancellationToken);

    public async Task<EnergyStats> GetStatsAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var readings = await _context.EnergyReadings.Where(r => r.Timestamp >= from && r.Timestamp <= to).ToListAsync(cancellationToken);
        if (!readings.Any()) return new EnergyStats { From = from, To = to };

        var first = readings.First();
        var last = readings.Last();
        return new EnergyStats
        {
            From = from, To = to,
            TotalConsumed = (last.ElectricityDeliveredTariff1 - first.ElectricityDeliveredTariff1) + (last.ElectricityDeliveredTariff2 - first.ElectricityDeliveredTariff2),
            TotalProduced = (last.ElectricityReturnedTariff1 - first.ElectricityReturnedTariff1) + (last.ElectricityReturnedTariff2 - first.ElectricityReturnedTariff2),
            PeakConsumption = readings.Max(r => r.CurrentPowerUsage),
            PeakProduction = readings.Max(r => r.CurrentPowerDelivery),
            AverageConsumption = readings.Average(r => r.CurrentPowerUsage),
            AverageProduction = readings.Average(r => r.CurrentPowerDelivery),
            ReadingCount = readings.Count
        };
    }

    public async Task<IEnumerable<EnergyReading>> GetHourlyAveragesAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var readings = await _context.EnergyReadings.Where(r => r.Timestamp >= from && r.Timestamp <= to).OrderBy(r => r.Timestamp).ToListAsync(cancellationToken);
        return readings
            .GroupBy(r => new DateTime(r.Timestamp.Year, r.Timestamp.Month, r.Timestamp.Day, r.Timestamp.Hour, 0, 0, DateTimeKind.Utc))
            .Select(g => new EnergyReading
            {
                Timestamp = g.Key,
                CurrentPowerUsage = g.Average(r => r.CurrentPowerUsage),
                CurrentPowerDelivery = g.Average(r => r.CurrentPowerDelivery),
                ElectricityDeliveredTariff1 = g.Last().ElectricityDeliveredTariff1,
                ElectricityDeliveredTariff2 = g.Last().ElectricityDeliveredTariff2,
                ElectricityReturnedTariff1 = g.Last().ElectricityReturnedTariff1,
                ElectricityReturnedTariff2 = g.Last().ElectricityReturnedTariff2,
                GasDelivered = g.Last().GasDelivered
            }).ToList();
    }
}
