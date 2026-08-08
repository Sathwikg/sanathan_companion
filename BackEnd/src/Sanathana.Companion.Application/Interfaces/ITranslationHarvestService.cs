using Sanathana.Companion.Application.DTOs.Localization;

namespace Sanathana.Companion.Application.Interfaces;

public interface ITranslationHarvestService
{
    /// <summary>
    /// Scans every registered column for values the dictionary does not know yet and adds them as
    /// untranslated terms. Idempotent.
    /// </summary>
    Task<HarvestResultDto> HarvestAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads distinct values from a registered table/column. Implemented in Infrastructure because it
/// needs the EF model to validate the identifiers before they reach SQL.
/// </summary>
public interface IVocabularyColumnReader
{
    /// <summary>
    /// Distinct non-empty values of one column, or an empty list when the table/column is not part
    /// of the EF model (which is what makes an injected identifier impossible).
    /// </summary>
    Task<IReadOnlyList<string>> ReadDistinctAsync(string tableName, string columnName, int max, CancellationToken cancellationToken = default);
}
