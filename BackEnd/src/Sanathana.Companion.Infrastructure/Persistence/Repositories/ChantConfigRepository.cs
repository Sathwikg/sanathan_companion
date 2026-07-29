using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Infrastructure.Persistence.Repositories;

public class ChantConfigRepository : BaseRepository<ChantConfig>, IChantConfigRepository
{
    public ChantConfigRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IReadOnlyList<ChantConfig>> GetFilteredAsync(
        Guid? chantId,
        Guid? deityId,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var query = Set.AsNoTracking().Include(c => c.Chant).AsQueryable();

        if (chantId is not null)
            query = query.Where(c => c.ChantId == chantId);

        if (deityId is not null)
        {
            // DeityIds is a comma-separated list; pad both sides so we match whole ids only.
            var needle = $"%,{deityId},%";
            query = query.Where(c => EF.Functions.Like("," + c.DeityIds + ",", needle));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = SqlLike.Contains(search);
            query = query.Where(c =>
                EF.Functions.ILike(c.Name, term, SqlLike.EscapeChar) ||
                (c.Description != null && EF.Functions.ILike(c.Description, term, SqlLike.EscapeChar)) ||
                (c.TimeDescription != null && EF.Functions.ILike(c.TimeDescription, term, SqlLike.EscapeChar)));
        }

        return await query.OrderBy(c => c.Name).ToListAsync(cancellationToken);
    }

    public async Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
        => await Set.AnyAsync(c => c.Name == name && (excludeId == null || c.Id != excludeId), cancellationToken);

    public async Task<ChantConfig?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking().Include(c => c.Chant).FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<(byte[]? Data, string? ContentType, string? FileName)> GetAudioAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var row = await Set.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new { Data = c.Audio != null ? c.Audio.Data : null, c.AudioContentType, c.AudioFileName })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? (null, null, null) : (row.Data, row.AudioContentType, row.AudioFileName);
    }

    public async Task<ChantConfigAudio?> GetAudioEntityAsync(Guid id, CancellationToken cancellationToken = default)
        => await Context.Set<ChantConfigAudio>().FirstOrDefaultAsync(a => a.ChantConfigId == id, cancellationToken);

    public async Task AddAudioAsync(ChantConfigAudio audio, CancellationToken cancellationToken = default)
        => await Context.Set<ChantConfigAudio>().AddAsync(audio, cancellationToken);

    public void RemoveAudio(ChantConfigAudio audio)
        => Context.Set<ChantConfigAudio>().Remove(audio);

    public async Task<IReadOnlyList<ChantLanguageConfig>> GetLanguageTextsAsync(Guid chantConfigId, CancellationToken cancellationToken = default)
        => await Context.Set<ChantLanguageConfig>()
            .Where(x => x.ChantConfigId == chantConfigId).ToListAsync(cancellationToken);

    public async Task AddLanguageTextAsync(ChantLanguageConfig entity, CancellationToken cancellationToken = default)
        => await Context.Set<ChantLanguageConfig>().AddAsync(entity, cancellationToken);

    public void UpdateLanguageText(ChantLanguageConfig entity)
        => Context.Set<ChantLanguageConfig>().Update(entity);

    public void RemoveLanguageText(ChantLanguageConfig entity)
        => Context.Set<ChantLanguageConfig>().Remove(entity);
}
