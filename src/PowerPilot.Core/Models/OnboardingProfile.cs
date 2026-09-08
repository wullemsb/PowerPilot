namespace PowerPilot.Core.Models;

public class OnboardingProfile
{
    public int Id { get; set; }
    public bool IsCompleted { get; set; }
    public bool HasSolarPanels { get; set; }
    public decimal? SolarCapacityKw { get; set; }
    public string TariffType { get; set; } = string.Empty;
    public decimal? ElectricityRate { get; set; }
    public decimal? FeedInRate { get; set; }
    public int HouseholdSize { get; set; }
    public string HomeType { get; set; } = string.Empty;
    public string MajorAppliances { get; set; } = string.Empty;
    public string Goals { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}