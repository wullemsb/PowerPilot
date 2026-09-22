using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PowerPilot.Core.Models;
using PowerPilot.Infrastructure.Data;

namespace PowerPilot.Tests;

public sealed class EnergyDatabaseInitializerTests
{
    [Fact]
    public async Task InitializeAsync_AddsOnboardingProfilesToLegacyDatabase()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE EnergyReadings (
                    Id INTEGER NOT NULL CONSTRAINT PK_EnergyReadings PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT NOT NULL
                );
                INSERT INTO EnergyReadings (Timestamp) VALUES ('2026-09-21T00:00:00');
                """;
            await command.ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<EnergyDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new EnergyDbContext(options);

        await EnergyDatabaseInitializer.InitializeAsync(context);

        var repository = new OnboardingRepository(context);
        await repository.SaveProfileAsync(new OnboardingProfile
        {
            IsCompleted = true,
            HasSolarPanels = true,
            SolarCapacityKw = 4.2m,
            TariffType = "Single",
            ElectricityRate = 0.25m,
            FeedInRate = 0.08m,
            HouseholdSize = 2,
            HomeType = "Apartment",
            MajorAppliances = "Heat pump",
            Goals = "Lower my energy bill",
            CompletedAt = DateTime.UtcNow
        });

        var savedProfile = await repository.GetProfileAsync();
        Assert.NotNull(savedProfile);
        Assert.Equal("Heat pump", savedProfile.MajorAppliances);

        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM EnergyReadings";
        Assert.Equal(1L, (long)(await countCommand.ExecuteScalarAsync())!);
    }
}