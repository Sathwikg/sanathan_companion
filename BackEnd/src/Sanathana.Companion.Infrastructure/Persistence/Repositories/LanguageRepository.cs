using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Infrastructure.Persistence.Repositories;

public class LanguageRepository : BaseRepository<Language>, ILanguageRepository
{
    public LanguageRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Language>> GetAllOrderedAsync(CancellationToken cancellationToken = default)
        => await Set.AsNoTracking().OrderBy(l => l.Name).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Language>> GetFilteredAsync(
        Guid? regionId,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var query = Set.AsNoTracking().AsQueryable();

        if (regionId is not null)
        {
            // Regions is a comma-separated list; pad both sides so we match whole ids only.
            var needle = $"%,{regionId},%";
            query = query.Where(l => EF.Functions.Like("," + l.Regions + ",", needle));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = SqlLike.Contains(search);
            query = query.Where(l =>
                EF.Functions.ILike(l.Name, term, SqlLike.EscapeChar) ||
                (l.NativeName != null && EF.Functions.ILike(l.NativeName, term, SqlLike.EscapeChar)) ||
                (l.Code != null && EF.Functions.ILike(l.Code, term, SqlLike.EscapeChar)) ||
                (l.Description != null && EF.Functions.ILike(l.Description, term, SqlLike.EscapeChar)));
        }

        return await query.OrderBy(l => l.Name).ToListAsync(cancellationToken);
    }

    public async Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
        => await Set.AnyAsync(l => l.Name == name && (excludeId == null || l.Id != excludeId), cancellationToken);
}
