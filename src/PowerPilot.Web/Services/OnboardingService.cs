using PowerPilot.Core.Interfaces;
using PowerPilot.Core.Models;

namespace PowerPilot.Web.Services;

public sealed class OnboardingService
{
    private readonly IOnboardingRepository _repository;

    public OnboardingService(IOnboardingRepository repository)
    {
        _repository = repository;
    }

    public Task<OnboardingProfile?> GetProfileAsync(CancellationToken cancellationToken = default)
        => _repository.GetProfileAsync(cancellationToken);

    public IReadOnlyList<string> Validate(OnboardingProfile profile)
    {
        var errors = new List<string>();

        if (profile.HasSolarPanels && (!profile.SolarCapacityKw.HasValue || profile.SolarCapacityKw <= 0))
            errors.Add("Enter a solar capacity greater than zero when solar panels are installed.");

        if (string.IsNullOrWhiteSpace(profile.TariffType))
            errors.Add("Select your tariff type.");

        if (profile.HouseholdSize <= 0)
            errors.Add("Enter the number of people in your household.");

        if (string.IsNullOrWhiteSpace(profile.HomeType))
            errors.Add("Select your home type.");

        if (string.IsNullOrWhiteSpace(profile.Goals))
            errors.Add("Select at least one energy goal.");

        return errors;
    }

    public Task SaveAsync(OnboardingProfile profile, CancellationToken cancellationToken = default)
    {
        profile.IsCompleted = true;
        profile.CompletedAt ??= DateTime.UtcNow;
        return _repository.SaveProfileAsync(profile, cancellationToken);
    }
}