using Microsoft.EntityFrameworkCore;
using PowerPilot.Core.Models;

namespace PowerPilot.Infrastructure.Data;

public class EnergyDbContext : DbContext
{
    public EnergyDbContext(DbContextOptions<EnergyDbContext> options) : base(options) { }
    public DbSet<EnergyReading> EnergyReadings => Set<EnergyReading>();
    public DbSet<OnboardingProfile> OnboardingProfiles => Set<OnboardingProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EnergyReading>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Timestamp);
            entity.Property(e => e.ElectricityDeliveredTariff1).HasColumnType("decimal(10,3)");
            entity.Property(e => e.ElectricityDeliveredTariff2).HasColumnType("decimal(10,3)");
            entity.Property(e => e.ElectricityReturnedTariff1).HasColumnType("decimal(10,3)");
            entity.Property(e => e.ElectricityReturnedTariff2).HasColumnType("decimal(10,3)");
            entity.Property(e => e.CurrentPowerUsage).HasColumnType("decimal(6,3)");
            entity.Property(e => e.CurrentPowerDelivery).HasColumnType("decimal(6,3)");
            entity.Property(e => e.GasDelivered).HasColumnType("decimal(10,3)");
            entity.Ignore(e => e.NetPower);
            entity.Ignore(e => e.IsProducing);
        });

        modelBuilder.Entity<OnboardingProfile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TariffType).HasMaxLength(32).IsRequired();
            entity.Property(e => e.HomeType).HasMaxLength(64).IsRequired();
            entity.Property(e => e.MajorAppliances).HasMaxLength(512).IsRequired();
            entity.Property(e => e.Goals).HasMaxLength(512).IsRequired();
            entity.Property(e => e.SolarCapacityKw).HasColumnType("decimal(8,3)");
            entity.Property(e => e.ElectricityRate).HasColumnType("decimal(8,4)");
            entity.Property(e => e.FeedInRate).HasColumnType("decimal(8,4)");
        });
    }
}
