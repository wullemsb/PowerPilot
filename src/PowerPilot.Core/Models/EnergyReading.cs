namespace PowerPilot.Core.Models;

public class EnergyReading
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; }
    public decimal ElectricityDeliveredTariff1 { get; set; }
    public decimal ElectricityDeliveredTariff2 { get; set; }
    public decimal ElectricityReturnedTariff1 { get; set; }
    public decimal ElectricityReturnedTariff2 { get; set; }
    public decimal CurrentPowerUsage { get; set; }
    public decimal CurrentPowerDelivery { get; set; }
    public decimal GasDelivered { get; set; }
    public decimal NetPower => CurrentPowerDelivery - CurrentPowerUsage;
    public bool IsProducing => NetPower > 0;
}
