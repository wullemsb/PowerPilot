using PowerPilot.Core.Interfaces;
using PowerPilot.Core.Models;
using PowerPilot.Web.Services;

namespace PowerPilot.Tests;

public sealed class OnboardingServiceTests
{
    [Fact]
    public void Validate_RequiresSolarCapacityWhenSolarIsEnabled()
    {
        var service = new OnboardingService(new StubRepository());
        var profile = ValidProfile();
        profile.HasSolarPanels = true;

        var errors = service.Validate(profile);

        Assert.Contains(errors, error => error.Contains("solar capacity", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_AcceptsACompleteProfile()
    {
        var service = new OnboardingService(new StubRepository());

        Assert.Empty(service.Validate(ValidProfile()));
    }

    private static OnboardingProfile ValidProfile() => new()
    {
        TariffType = "Single",
        HouseholdSize = 2,
        HomeType = "Apartment",
        Goals = "Lower my energy bill"
    };

    private sealed class StubRepository : IOnboardingRepository
    {
        public Task<OnboardingProfile?> GetProfileAsync(CancellationToken cancellationToken = default) => Task.FromResult<OnboardingProfile?>(null);
        public Task<bool> IsCompletedAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task SaveProfileAsync(OnboardingProfile profile, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}