using Microsoft.EntityFrameworkCore;
using PowerPilot.Core.Interfaces;
using PowerPilot.Core.Models;

namespace PowerPilot.Infrastructure.Data;

public sealed class OnboardingRepository : IOnboardingRepository
{
    private readonly EnergyDbContext _context;

    public OnboardingRepository(EnergyDbContext context)
    {
        _context = context;
    }

    public Task<OnboardingProfile?> GetProfileAsync(CancellationToken cancellationToken = default)
        => _context.OnboardingProfiles.OrderBy(profile => profile.Id).FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> IsCompletedAsync(CancellationToken cancellationToken = default)
        => await _context.OnboardingProfiles.AnyAsync(profile => profile.IsCompleted, cancellationToken);

    public async Task SaveProfileAsync(OnboardingProfile profile, CancellationToken cancellationToken = default)
    {
        profile.UpdatedAt = DateTime.UtcNow;
        var existing = await GetProfileAsync(cancellationToken);
        if (existing is null)
        {
            profile.Id = 1;
            profile.CreatedAt = DateTime.UtcNow;
            _context.OnboardingProfiles.Add(profile);
        }
        else
        {
            profile.Id = existing.Id;
            profile.CreatedAt = existing.CreatedAt;
            _context.Entry(existing).CurrentValues.SetValues(profile);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}