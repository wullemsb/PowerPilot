using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PowerPilot.Core.Models;
using PowerPilot.Infrastructure.Data;

namespace PowerPilot.Tests;

public sealed class OnboardingRepositoryTests
{
    [Fact]
    public async Task IsCompletedAsync_ReturnsFalseUntilProfileIsCompleted()
    {
        await using var fixture = await Fixture.CreateAsync();

        Assert.False(await fixture.Repository.IsCompletedAsync());

        await fixture.Repository.SaveProfileAsync(CreateProfile());

        Assert.True(await fixture.Repository.IsCompletedAsync());
    }

    [Fact]
    public async Task SaveProfileAsync_UpdatesTheExistingProfile()
    {
        await using var fixture = await Fixture.CreateAsync();
        var profile = CreateProfile();
        await fixture.Repository.SaveProfileAsync(profile);

        profile.HouseholdSize = 4;
        profile.HomeType = "Detached";
        await fixture.Repository.SaveProfileAsync(profile);

        var saved = await fixture.Repository.GetProfileAsync();
        Assert.NotNull(saved);
        Assert.Equal(4, saved.HouseholdSize);
        Assert.Equal("Detached", saved.HomeType);
        Assert.Equal(1, await fixture.Context.OnboardingProfiles.CountAsync());
    }

    private static OnboardingProfile CreateProfile() => new()
    {
        IsCompleted = true,
        HasSolarPanels = true,
        SolarCapacityKw = 4.2m,
        TariffType = "Single",
        HouseholdSize = 2,
        HomeType = "Apartment",
        Goals = "Lower my energy bill"
    };

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private Fixture(SqliteConnection connection, EnergyDbContext context)
        {
            this.connection = connection;
            Context = context;
            Repository = new OnboardingRepository(context);
        }

        public EnergyDbContext Context { get; }
        public OnboardingRepository Repository { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<EnergyDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new EnergyDbContext(options);
            await context.Database.EnsureCreatedAsync();
            return new Fixture(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}