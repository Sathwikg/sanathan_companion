using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Infrastructure.Persistence.Repositories;

public class LocalizationRepository : ILocalizationRepository
{
    private readonly ApplicationDbContext _context;

    public LocalizationRepository(ApplicationDbContext context) => _context = context;

    private DbSet<LocalizationResource> Resources => _context.Set<LocalizationResource>();
    private DbSet<EntityTranslation> Entities => _context.Set<EntityTranslation>();
    private DbSet<LanguageFormConfig> FormConfigs => _context.Set<LanguageFormConfig>();

    // ---- UI labels ----

    public async Task<IReadOnlyList<LocalizationResource>> GetResourcesAsync(Guid languageId, CancellationToken cancellationToken = default)
        => await Resources.AsNoTracking().Where(x => x.LanguageId == languageId).ToListAsync(cancellationToken);

    public async Task<List<LocalizationResource>> GetResourcesTrackedAsync(Guid languageId, CancellationToken cancellationToken = default)
        => await Resources.Where(x => x.LanguageId == languageId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> GetAllKeysAsync(CancellationToken cancellationToken = default)
        => await Resources.AsNoTracking().Select(x => x.Key).Distinct().OrderBy(k => k).ToListAsync(cancellationToken);

    public async Task AddResourceAsync(LocalizationResource entity, CancellationToken cancellationToken = default)
        => await Resources.AddAsync(entity, cancellationToken);

    public void UpdateResource(LocalizationResource entity) => Resources.Update(entity);
    public void RemoveResource(LocalizationResource entity) => Resources.Remove(entity);

    // ---- DB-content translations ----

    public async Task<IReadOnlyList<EntityTranslation>> GetEntityTranslationsAsync(Guid languageId, CancellationToken cancellationToken = default)
        => await Entities.AsNoTracking().Where(x => x.LanguageId == languageId).ToListAsync(cancellationToken);

    public async Task<List<EntityTranslation>> GetEntityTranslationsTrackedAsync(Guid languageId, CancellationToken cancellationToken = default)
        => await Entities.Where(x => x.LanguageId == languageId).ToListAsync(cancellationToken);

    public async Task AddEntityTranslationAsync(EntityTranslation entity, CancellationToken cancellationToken = default)
        => await Entities.AddAsync(entity, cancellationToken);

    public void UpdateEntityTranslation(EntityTranslation entity) => Entities.Update(entity);
    public void RemoveEntityTranslation(EntityTranslation entity) => Entities.Remove(entity);

    // ---- Shared term dictionary ----

    private DbSet<TranslationTerm> Terms => _context.Set<TranslationTerm>();
    private DbSet<TranslationTermText> TermTexts => _context.Set<TranslationTermText>();
    private DbSet<TranslationSource> Sources => _context.Set<TranslationSource>();

    public async Task<IReadOnlyList<(TranslationTerm Term, string? Text)>> GetTermsForLanguageAsync(
        Guid languageId, CancellationToken cancellationToken = default)
    {
        // Left join so the caller can also see which terms are still untranslated.
        var rows = await Terms.AsNoTracking()
            .Where(t => t.IsActive)
            .Select(t => new
            {
                Term = t,
                Text = t.Texts.Where(x => x.LanguageId == languageId).Select(x => x.Text).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => (r.Term, r.Text)).ToList();
    }

    public async Task<List<TranslationTerm>> GetTermsTrackedAsync(CancellationToken cancellationToken = default)
        => await Terms.Include(t => t.Texts).ToListAsync(cancellationToken);

    public async Task<HashSet<string>> GetTermKeysAsync(CancellationToken cancellationToken = default)
        => (await Terms.AsNoTracking().Select(t => t.TermKey).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

    public async Task AddTermAsync(TranslationTerm term, CancellationToken cancellationToken = default)
        => await Terms.AddAsync(term, cancellationToken);

    public void UpdateTerm(TranslationTerm term) => Terms.Update(term);

    public async Task<List<TranslationTermText>> GetTermTextsTrackedAsync(Guid languageId, CancellationToken cancellationToken = default)
        => await TermTexts.Where(x => x.LanguageId == languageId).ToListAsync(cancellationToken);

    public async Task AddTermTextAsync(TranslationTermText text, CancellationToken cancellationToken = default)
        => await TermTexts.AddAsync(text, cancellationToken);

    public void UpdateTermText(TranslationTermText text) => TermTexts.Update(text);
    public void RemoveTermText(TranslationTermText text) => TermTexts.Remove(text);

    public async Task<IReadOnlyList<TranslationSource>> GetTranslationSourcesAsync(CancellationToken cancellationToken = default)
        => await Sources.AsNoTracking().Where(s => s.IsActive).ToListAsync(cancellationToken);

    // ---- Per-form enablement ----

    public async Task<IReadOnlyList<LanguageFormConfig>> GetFormConfigsAsync(Guid languageId, CancellationToken cancellationToken = default)
        => await FormConfigs.AsNoTracking().Where(x => x.LanguageId == languageId).ToListAsync(cancellationToken);

    public async Task<List<LanguageFormConfig>> GetFormConfigsTrackedAsync(Guid languageId, CancellationToken cancellationToken = default)
        => await FormConfigs.Where(x => x.LanguageId == languageId).ToListAsync(cancellationToken);

    public async Task AddFormConfigAsync(LanguageFormConfig entity, CancellationToken cancellationToken = default)
        => await FormConfigs.AddAsync(entity, cancellationToken);

    public void UpdateFormConfig(LanguageFormConfig entity) => FormConfigs.Update(entity);
    public void RemoveFormConfig(LanguageFormConfig entity) => FormConfigs.Remove(entity);
}
