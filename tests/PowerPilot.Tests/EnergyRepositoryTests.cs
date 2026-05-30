using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PowerPilot.Core.Models;
using PowerPilot.Infrastructure.Data;

namespace PowerPilot.Tests;

public sealed class EnergyRepositoryTests
{
    [Fact]
    public async Task GetStatsAsync_UsesChronologicalReadings()
    {
        await using var fixture = await EnergyRepositoryFixture.CreateAsync();
        fixture.Context.EnergyReadings.AddRange(
            CreateReading(new DateTime(2024, 1, 1, 10, 20, 0, DateTimeKind.Utc), 120m, 80m, 10m, 5m, 3m, 0m),
            CreateReading(new DateTime(2024, 1, 1, 10, 00, 0, DateTimeKind.Utc), 100m, 50m, 5m, 1m, 2m, 0m),
            CreateReading(new DateTime(2024, 1, 1, 10, 10, 0, DateTimeKind.Utc), 110m, 60m, 6m, 2m, 4m, 1m));
        await fixture.Context.SaveChangesAsync();

        var stats = await fixture.Repository.GetStatsAsync(
            new DateTime(2024, 1, 1, 10, 00, 0, DateTimeKind.Utc),
            new DateTime(2024, 1, 1, 10, 30, 0, DateTimeKind.Utc));

        Assert.Equal(50m, stats.TotalConsumed);
        Assert.Equal(9m, stats.TotalProduced);
        Assert.Equal(-41m, stats.NetBalance);
        Assert.Equal(4m, stats.PeakConsumption);
        Assert.Equal(1m, stats.PeakProduction);
        Assert.Equal(3m, stats.AverageConsumption);
        Assert.Equal(1m / 3m, stats.AverageProduction);
        Assert.Equal(3, stats.ReadingCount);
    }

    [Fact]
    public async Task GetHourlyAveragesAsync_GroupsReadingsPerUtcHour()
    {
        await using var fixture = await EnergyRepositoryFixture.CreateAsync();
        fixture.Context.EnergyReadings.AddRange(
            CreateReading(new DateTime(2024, 1, 1, 10, 05, 0, DateTimeKind.Utc), 100m, 50m, 5m, 1m, 2m, 0m),
            CreateReading(new DateTime(2024, 1, 1, 10, 35, 0, DateTimeKind.Utc), 110m, 60m, 6m, 2m, 4m, 1m),
            CreateReading(new DateTime(2024, 1, 1, 11, 10, 0, DateTimeKind.Utc), 125m, 75m, 9m, 4m, 1m, 3m));
        await fixture.Context.SaveChangesAsync();

        var hourly = (await fixture.Repository.GetHourlyAveragesAsync(
            new DateTime(2024, 1, 1, 10, 00, 0, DateTimeKind.Utc),
            new DateTime(2024, 1, 1, 12, 00, 0, DateTimeKind.Utc))).ToList();

        Assert.Equal(2, hourly.Count);

        Assert.Equal(new DateTime(2024, 1, 1, 10, 00, 0, DateTimeKind.Utc), hourly[0].Timestamp);
        Assert.Equal(3m, hourly[0].CurrentPowerUsage);
        Assert.Equal(0.5m, hourly[0].CurrentPowerDelivery);
        Assert.Equal(110m, hourly[0].ElectricityDeliveredTariff1);
        Assert.Equal(60m, hourly[0].ElectricityDeliveredTariff2);

        Assert.Equal(new DateTime(2024, 1, 1, 11, 00, 0, DateTimeKind.Utc), hourly[1].Timestamp);
        Assert.Equal(1m, hourly[1].CurrentPowerUsage);
        Assert.Equal(3m, hourly[1].CurrentPowerDelivery);
    }

    private static EnergyReading CreateReading(
        DateTime timestamp,
        decimal deliveredT1,
        decimal deliveredT2,
        decimal returnedT1,
        decimal returnedT2,
        decimal currentUsage,
        decimal currentDelivery) =>
        new()
        {
            Timestamp = timestamp,
            ElectricityDeliveredTariff1 = deliveredT1,
            ElectricityDeliveredTariff2 = deliveredT2,
            ElectricityReturnedTariff1 = returnedT1,
            ElectricityReturnedTariff2 = returnedT2,
            CurrentPowerUsage = currentUsage,
            CurrentPowerDelivery = currentDelivery
        };

    private sealed class EnergyRepositoryFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private EnergyRepositoryFixture(SqliteConnection connection, EnergyDbContext context)
        {
            _connection = connection;
            Context = context;
            Repository = new EnergyRepository(context);
        }

        public EnergyDbContext Context { get; }

        public EnergyRepository Repository { get; }

        public static async Task<EnergyRepositoryFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<EnergyDbContext>()
                .UseSqlite(connection)
                .Options;

            var context = new EnergyDbContext(options);
            await context.Database.EnsureCreatedAsync();

            return new EnergyRepositoryFixture(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
