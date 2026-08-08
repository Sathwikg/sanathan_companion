using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Domain.Interfaces;

public interface ILocalizationRepository
{
    // ---- UI labels ----
    Task<IReadOnlyList<LocalizationResource>> GetResourcesAsync(Guid languageId, CancellationToken cancellationToken = default);
    Task<List<LocalizationResource>> GetResourcesTrackedAsync(Guid languageId, CancellationToken cancellationToken = default);
    /// <summary>Every distinct key that exists in any language — used to build the editor grid.</summary>
    Task<IReadOnlyList<string>> GetAllKeysAsync(CancellationToken cancellationToken = default);
    Task AddResourceAsync(LocalizationResource entity, CancellationToken cancellationToken = default);
    void UpdateResource(LocalizationResource entity);
    void RemoveResource(LocalizationResource entity);

    // ---- DB-content translations ----
    Task<IReadOnlyList<EntityTranslation>> GetEntityTranslationsAsync(Guid languageId, CancellationToken cancellationToken = default);
    Task<List<EntityTranslation>> GetEntityTranslationsTrackedAsync(Guid languageId, CancellationToken cancellationToken = default);
    Task AddEntityTranslationAsync(EntityTranslation entity, CancellationToken cancellationToken = default);
    void UpdateEntityTranslation(EntityTranslation entity);
    void RemoveEntityTranslation(EntityTranslation entity);

    // ---- Shared term dictionary ----
    /// <summary>Every active term with its text for one language (no tracking) — feeds the matcher.</summary>
    Task<IReadOnlyList<(TranslationTerm Term, string? Text)>> GetTermsForLanguageAsync(Guid languageId, CancellationToken cancellationToken = default);
    /// <summary>All terms, tracked, for the harvest and save paths.</summary>
    Task<List<TranslationTerm>> GetTermsTrackedAsync(CancellationToken cancellationToken = default);
    /// <summary>Just the normalised keys — used by the harvester to skip what it already knows.</summary>
    Task<HashSet<string>> GetTermKeysAsync(CancellationToken cancellationToken = default);
    Task AddTermAsync(TranslationTerm term, CancellationToken cancellationToken = default);
    void UpdateTerm(TranslationTerm term);
    Task<List<TranslationTermText>> GetTermTextsTrackedAsync(Guid languageId, CancellationToken cancellationToken = default);
    Task AddTermTextAsync(TranslationTermText text, CancellationToken cancellationToken = default);
    void UpdateTermText(TranslationTermText text);
    void RemoveTermText(TranslationTermText text);

    /// <summary>Columns registered for harvesting.</summary>
    Task<IReadOnlyList<TranslationSource>> GetTranslationSourcesAsync(CancellationToken cancellationToken = default);

    // ---- Per-form enablement ----
    Task<IReadOnlyList<LanguageFormConfig>> GetFormConfigsAsync(Guid languageId, CancellationToken cancellationToken = default);
    Task<List<LanguageFormConfig>> GetFormConfigsTrackedAsync(Guid languageId, CancellationToken cancellationToken = default);
    Task AddFormConfigAsync(LanguageFormConfig entity, CancellationToken cancellationToken = default);
    void UpdateFormConfig(LanguageFormConfig entity);
    void RemoveFormConfig(LanguageFormConfig entity);
}
