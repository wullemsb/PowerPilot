namespace PowerPilot.Core.Models;

public class EnergyStats
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public decimal TotalConsumed { get; set; }
    public decimal TotalProduced { get; set; }
    public decimal NetBalance => TotalProduced - TotalConsumed;
    public decimal PeakConsumption { get; set; }
    public decimal PeakProduction { get; set; }
    public decimal AverageConsumption { get; set; }
    public decimal AverageProduction { get; set; }
    public int ReadingCount { get; set; }
}
