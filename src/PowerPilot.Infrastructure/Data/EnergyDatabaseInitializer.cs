using Microsoft.EntityFrameworkCore;

namespace PowerPilot.Infrastructure.Data;

public static class EnergyDatabaseInitializer
{
    public static async Task InitializeAsync(
        EnergyDbContext context,
        CancellationToken cancellationToken = default)
    {
        await context.Database.EnsureCreatedAsync(cancellationToken);

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "OnboardingProfiles" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_OnboardingProfiles" PRIMARY KEY AUTOINCREMENT,
                "IsCompleted" INTEGER NOT NULL,
                "HasSolarPanels" INTEGER NOT NULL,
                "SolarCapacityKw" decimal(8,3) NULL,
                "TariffType" TEXT NOT NULL,
                "ElectricityRate" decimal(8,4) NULL,
                "FeedInRate" decimal(8,4) NULL,
                "HouseholdSize" INTEGER NOT NULL,
                "HomeType" TEXT NOT NULL,
                "MajorAppliances" TEXT NOT NULL,
                "Goals" TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL,
                "CompletedAt" TEXT NULL
            );
            """,
            cancellationToken);
    }
}