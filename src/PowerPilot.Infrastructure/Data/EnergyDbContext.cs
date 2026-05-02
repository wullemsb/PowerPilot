using Microsoft.EntityFrameworkCore;
using PowerPilot.Core.Models;

namespace PowerPilot.Infrastructure.Data;

public class EnergyDbContext : DbContext
{
    public EnergyDbContext(DbContextOptions<EnergyDbContext> options) : base(options) { }
    public DbSet<EnergyReading> EnergyReadings => Set<EnergyReading>();

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
    }
}
