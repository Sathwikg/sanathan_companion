using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;
using Sanathana.Companion.Infrastructure.Seed;

namespace Sanathana.Companion.Infrastructure.Persistence.Repositories;

public class AdRepository : IAdRepository
{
    private readonly ApplicationDbContext _context;

    public AdRepository(ApplicationDbContext context) => _context = context;

    public async Task<IReadOnlyList<AdFormat>> GetFormatsAsync(CancellationToken cancellationToken = default)
        => await _context.AdFormats.AsNoTracking()
            .OrderBy(f => f.DisplayOrder).ThenBy(f => f.Name)
            .ToListAsync(cancellationToken);

    public async Task<AdFormat?> GetFormatByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.AdFormats.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AdPlacement>> GetPlacementsAsync(CancellationToken cancellationToken = default)
        => await _context.AdPlacements
            .Include(p => p.MenuModule)
            .Include(p => p.AdFormat)
            .ToListAsync(cancellationToken);

    public async Task<AdPlacement?> GetPlacementForModuleAsync(Guid menuModuleId, CancellationToken cancellationToken = default)
        => await _context.AdPlacements.AsNoTracking()
            .Include(p => p.AdFormat)
            .FirstOrDefaultAsync(p => p.MenuModuleId == menuModuleId, cancellationToken);

    public async Task AddPlacementAsync(AdPlacement placement, CancellationToken cancellationToken = default)
        => await _context.AdPlacements.AddAsync(placement, cancellationToken);

    public void RemovePlacement(AdPlacement placement) => _context.AdPlacements.Remove(placement);

    public async Task<AdSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _context.AdSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings is not null) return settings;

        // Only reachable on a database whose seed predates this table. Created switched off, so a
        // missing row can never be the reason ads appear.
        settings = new AdSettings { Id = SeedConstants.AdSettingsId, AdsEnabled = false, UseTestAds = true };
        await _context.AdSettings.AddAsync(settings, cancellationToken);
        return settings;
    }
}
