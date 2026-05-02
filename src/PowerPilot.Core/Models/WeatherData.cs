namespace PowerPilot.Core.Models;

public class WeatherData
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Description { get; set; }
    public double TemperatureCelsius { get; set; }
    public double CloudCoverPercent { get; set; }
    public double WindSpeedMs { get; set; }
    public string? City { get; set; }
    public double SolarIrradianceEstimate => Math.Max(0, (1 - CloudCoverPercent / 100.0) * 1000);
}
