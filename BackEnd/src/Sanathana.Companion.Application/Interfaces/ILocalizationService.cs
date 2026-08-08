using Sanathana.Companion.Application.DTOs.Localization;

namespace Sanathana.Companion.Application.Interfaces;

public interface ILocalizationService
{
    /// <summary>Languages the user can switch into (active languages that have a bundle).</summary>
    Task<IReadOnlyList<LocaleDto>> GetLocalesAsync(CancellationToken cancellationToken = default);

    /// <summary>The merged bundle for a language code, with English filled in for anything missing.</summary>
    Task<LocalizationBundleDto> GetBundleAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>English-vs-target rows for the label editor.</summary>
    Task<LabelEditorDto> GetLabelEditorAsync(Guid languageId, CancellationToken cancellationToken = default);

    Task SaveLabelsAsync(Guid languageId, SaveLabelsDto dto, CancellationToken cancellationToken = default);

    Task<LanguageFormMatrixDto> GetFormMatrixAsync(Guid languageId, CancellationToken cancellationToken = default);

    Task SaveFormMatrixAsync(Guid languageId, SaveLanguageFormsDto dto, CancellationToken cancellationToken = default);

    /// <summary>Translatable DB content (menu names, deities, festivals…) with their English originals.</summary>
    Task<IReadOnlyList<EntityTranslationRowDto>> GetEntityRowsAsync(Guid languageId, CancellationToken cancellationToken = default);

    Task SaveEntityRowsAsync(Guid languageId, SaveEntityTranslationsDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every language side by side for one scope (a form's namespace, or a shared section).
    /// Pass null for <paramref name="scope"/> to get the scope list without any rows.
    /// </summary>
    Task<TranslationMatrixDto> GetMatrixAsync(string? scope, CancellationToken cancellationToken = default);

    /// <summary>Writes a whole grid — many keys across many languages — in one transaction.</summary>
    Task SaveMatrixAsync(SaveMatrixDto dto, CancellationToken cancellationToken = default);

    /// <summary>DB-driven content across every language.</summary>
    Task<EntityMatrixDto> GetEntityMatrixAsync(CancellationToken cancellationToken = default);

    Task SaveEntityMatrixAsync(SaveEntityMatrixDto dto, CancellationToken cancellationToken = default);

    /// <summary>Re-reads the embedded seed files, filling gaps without clobbering hand edits.</summary>
    Task<int> ImportSeedFilesAsync(CancellationToken cancellationToken = default);

    /// <summary>The current DB state for a language as JSON files, keyed by file name.</summary>
    Task<Dictionary<string, string>> ExportJsonAsync(Guid languageId, CancellationToken cancellationToken = default);
}
