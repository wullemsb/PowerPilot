using PowerPilot.Core.Models;

namespace PowerPilot.Core.Interfaces;

public interface IOnboardingRepository
{
    Task<OnboardingProfile?> GetProfileAsync(CancellationToken cancellationToken = default);
    Task<bool> IsCompletedAsync(CancellationToken cancellationToken = default);
    Task SaveProfileAsync(OnboardingProfile profile, CancellationToken cancellationToken = default);
}