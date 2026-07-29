using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Infrastructure.Persistence.Repositories;

public class DeityRepository : BaseRepository<Deity>, IDeityRepository
{
    public DeityRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Deity>> ListWithoutImageAsync(CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
            .OrderBy(d => d.Name)
            // Project a subset so the (potentially large) image blob is never loaded for the list.
            .Select(d => new Deity
            {
                Id = d.Id,
                Name = d.Name,
                Description = d.Description,
                WelcomeNote = d.WelcomeNote,
                DeityType = d.DeityType,
                Regions = d.Regions,
                Festivals = d.Festivals,
                Days = d.Days,
                IsActive = d.IsActive,
                ImageContentType = d.ImageContentType
            })
            .ToListAsync(cancellationToken);

    public async Task<(byte[]? Data, string? ContentType)> GetImageAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await Set.AsNoTracking()
            .Where(d => d.Id == id)
            .Select(d => new { d.ImageData, d.ImageContentType })
            .FirstOrDefaultAsync(cancellationToken);
        return (row?.ImageData, row?.ImageContentType);
    }

    public async Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
        => await Set.AnyAsync(d => d.Name == name && (excludeId == null || d.Id != excludeId), cancellationToken);
}
