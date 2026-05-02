using PowerPilot.Core.Interfaces;
using PowerPilot.Core.Models;

namespace PowerPilot.Agents.Plugins;

public class EnergyPlugin
{
    private readonly IEnergyRepository _repository;
    private readonly IEnergyStateService _stateService;

    public EnergyPlugin(IEnergyRepository repository, IEnergyStateService stateService)
    {
        _repository = repository;
        _stateService = stateService;
    }

    public string GetCurrentPower()
    {
        var telegram = _stateService.CurrentTelegram;
        if (telegram == null) return "No current power data available.";
        return $"Current power: consuming {telegram.CurrentPowerUsage:F3} kW from grid, " +
               $"delivering {telegram.CurrentPowerDelivery:F3} kW to grid. " +
               $"Net: {(telegram.IsProducing ? "PRODUCING" : "CONSUMING")} {Math.Abs(telegram.NetPower):F3} kW. " +
               $"Tariff: {(telegram.CurrentTariff == 1 ? "Night (cheaper)" : "Day")}";
    }

    public async Task<string> GetTodayStatsAsync()
    {
        var today = DateTime.UtcNow.Date;
        var stats = await _repository.GetStatsAsync(today, DateTime.UtcNow);
        return $"Today's stats: Consumed {stats.TotalConsumed:F2} kWh, Produced {stats.TotalProduced:F2} kWh, " +
               $"Net balance: {stats.NetBalance:F2} kWh ({(stats.NetBalance >= 0 ? "surplus" : "deficit")}), " +
               $"Peak consumption: {stats.PeakConsumption:F2} kW, Peak production: {stats.PeakProduction:F2} kW";
    }

    public async Task<string> GetEnergyStatsAsync(string period = "today")
    {
        var (from, to) = period.ToLower() switch
        {
            "yesterday" => (DateTime.UtcNow.Date.AddDays(-1), DateTime.UtcNow.Date),
            "week" => (DateTime.UtcNow.Date.AddDays(-7), DateTime.UtcNow),
            "month" => (DateTime.UtcNow.Date.AddDays(-30), DateTime.UtcNow),
            _ => (DateTime.UtcNow.Date, DateTime.UtcNow)
        };
        var stats = await _repository.GetStatsAsync(from, to);
        return $"Energy stats for {period}: Consumed {stats.TotalConsumed:F2} kWh, Produced {stats.TotalProduced:F2} kWh, " +
               $"Net: {stats.NetBalance:F2} kWh, Peak consumption: {stats.PeakConsumption:F2} kW, Based on {stats.ReadingCount} readings";
    }

    public async Task<string> GetHourlyProfileAsync()
    {
        var readings = await _repository.GetHourlyAveragesAsync(DateTime.UtcNow.Date, DateTime.UtcNow);
        if (!readings.Any()) return "No hourly data available yet.";
        var profile = string.Join(", ", readings.Select(r =>
            $"{r.Timestamp.Hour:D2}h: {(r.CurrentPowerUsage > r.CurrentPowerDelivery ? "-" : "+")}{Math.Abs(r.NetPower):F2}kW"));
        return $"Hourly profile (+ = producing, - = consuming): {profile}";
    }

    public async Task<string> GetApplianceAdviceAsync(string appliance)
    {
        var telegram = _stateService.CurrentTelegram;
        var hourlyReadings = (await _repository.GetHourlyAveragesAsync(DateTime.UtcNow.Date.AddDays(-7), DateTime.UtcNow)).ToList();

        var appliancePower = appliance.ToLower() switch
        {
            var a when a.Contains("dishwasher") => 1.8m,
            var a when a.Contains("washing") => 2.0m,
            var a when a.Contains("dryer") => 3.0m,
            var a when a.Contains("ev") || a.Contains("car") => 7.4m,
            _ => 1.5m
        };

        var currentNet = telegram?.NetPower ?? 0;
        var isGoodTimeNow = currentNet > appliancePower * 0.5m;
        var advice = new System.Text.StringBuilder();
        advice.AppendLine($"Advice for {appliance} ({appliancePower:F1} kW):");
        advice.AppendLine($"Current net power: {currentNet:F2} kW ({(currentNet > 0 ? "producing" : "consuming")})");
        advice.AppendLine($"Right now: {(isGoodTimeNow ? "GOOD TIME" : "Not ideal")} to run {appliance}");

        if (hourlyReadings.Any())
        {
            var bestHours = hourlyReadings
                .GroupBy(r => r.Timestamp.Hour)
                .Select(g => new { Hour = g.Key, AvgNet = g.Average(r => r.NetPower) })
                .OrderByDescending(x => x.AvgNet)
                .Take(3).ToList();
            if (bestHours.Any())
                advice.AppendLine($"Best hours based on history: {string.Join(", ", bestHours.Select(h => $"{h.Hour:D2}:00 (avg {h.AvgNet:F2}kW)"))}");
        }
        return advice.ToString();
    }
}
