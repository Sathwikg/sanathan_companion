using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Infrastructure.Persistence.Repositories;

public class PujaProcessRepository : IPujaProcessRepository
{
    private readonly ApplicationDbContext _context;

    public PujaProcessRepository(ApplicationDbContext context) => _context = context;

    private DbSet<PujaMaterial> Materials => _context.Set<PujaMaterial>();
    private DbSet<PujaStep> Steps => _context.Set<PujaStep>();
    private DbSet<PujaStepText> Texts => _context.Set<PujaStepText>();
    private DbSet<UserPujaStepProgress> Progress => _context.Set<UserPujaStepProgress>();

    // ---- Materials ----

    public async Task<IReadOnlyList<PujaMaterial>> GetMaterialsAsync(Guid pujaId, CancellationToken cancellationToken = default)
        => await Materials.AsNoTracking()
            .Where(m => m.PujaId == pujaId)
            .OrderBy(m => m.DisplayOrder)
            .ToListAsync(cancellationToken);

    public async Task<List<PujaMaterial>> GetMaterialsTrackedAsync(Guid pujaId, CancellationToken cancellationToken = default)
        => await Materials.Where(m => m.PujaId == pujaId).ToListAsync(cancellationToken);

    public async Task AddMaterialAsync(PujaMaterial material, CancellationToken cancellationToken = default)
        => await Materials.AddAsync(material, cancellationToken);

    public void RemoveMaterial(PujaMaterial material) => Materials.Remove(material);

    // ---- Steps ----

    public async Task<IReadOnlyList<PujaStep>> GetStepsWithTextsAsync(Guid pujaId, CancellationToken cancellationToken = default)
        => await Steps.AsNoTracking()
            .Include(s => s.Texts)
            .Where(s => s.PujaId == pujaId)
            .OrderBy(s => s.StepNumber)
            .ToListAsync(cancellationToken);

    public async Task<List<PujaStep>> GetStepsTrackedAsync(Guid pujaId, CancellationToken cancellationToken = default)
        => await Steps.Include(s => s.Texts)
            .Where(s => s.PujaId == pujaId)
            .OrderBy(s => s.StepNumber)
            .ToListAsync(cancellationToken);

    public async Task AddStepAsync(PujaStep step, CancellationToken cancellationToken = default)
        => await Steps.AddAsync(step, cancellationToken);

    public void UpdateStep(PujaStep step) => Steps.Update(step);
    public void RemoveStep(PujaStep step) => Steps.Remove(step);

    public async Task AddStepTextAsync(PujaStepText text, CancellationToken cancellationToken = default)
        => await Texts.AddAsync(text, cancellationToken);

    public void UpdateStepText(PujaStepText text) => Texts.Update(text);
    public void RemoveStepText(PujaStepText text) => Texts.Remove(text);

    public async Task<IReadOnlyDictionary<Guid, int>> GetStepCountsAsync(CancellationToken cancellationToken = default)
        => await Steps.AsNoTracking()
            .GroupBy(s => s.PujaId)
            .Select(g => new { PujaId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PujaId, x => x.Count, cancellationToken);

    // ---- Per-user progress ----

    public async Task<IReadOnlyList<UserPujaStepProgress>> GetProgressAsync(
        Guid userId, Guid pujaId, CancellationToken cancellationToken = default)
        => await Progress.AsNoTracking()
            .Where(p => p.UserId == userId && p.PujaId == pujaId)
            .ToListAsync(cancellationToken);

    public async Task<UserPujaStepProgress?> GetProgressEntryAsync(
        Guid userId, Guid pujaStepId, CancellationToken cancellationToken = default)
        => await Progress.FirstOrDefaultAsync(
            p => p.UserId == userId && p.PujaStepId == pujaStepId, cancellationToken);

    public async Task AddProgressAsync(UserPujaStepProgress entry, CancellationToken cancellationToken = default)
        => await Progress.AddAsync(entry, cancellationToken);

    public void RemoveProgress(UserPujaStepProgress entry) => Progress.Remove(entry);

    public async Task<List<UserPujaStepProgress>> GetProgressTrackedAsync(
        Guid userId, Guid pujaId, CancellationToken cancellationToken = default)
        => await Progress.Where(p => p.UserId == userId && p.PujaId == pujaId).ToListAsync(cancellationToken);
}
