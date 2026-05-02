namespace PowerPilot.Core.Models;

/// <summary>
/// Represents a parsed P1 telegram from a DSMR smart meter (Belgian/Dutch)
/// </summary>
public class P1Telegram
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? EquipmentIdentifier { get; set; }
    public decimal ElectricityDeliveredTariff1 { get; set; }
    public decimal ElectricityDeliveredTariff2 { get; set; }
    public decimal ElectricityReturnedTariff1 { get; set; }
    public decimal ElectricityReturnedTariff2 { get; set; }
    public int CurrentTariff { get; set; }
    public decimal CurrentPowerUsage { get; set; }
    public decimal CurrentPowerDelivery { get; set; }
    public decimal GasDelivered { get; set; }
    public decimal NetPower => CurrentPowerDelivery - CurrentPowerUsage;
    public bool IsProducing => NetPower > 0;
}
