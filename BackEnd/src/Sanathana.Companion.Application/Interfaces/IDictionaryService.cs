using Sanathana.Companion.Application.DTOs.Localization;

namespace Sanathana.Companion.Application.Interfaces;

public interface IDictionaryService
{
    /// <summary>
    /// One page of the term dictionary, every language side by side. Paged server-side because
    /// runtime misses can grow the table well past what one payload should carry.
    /// </summary>
    Task<DictionaryPageDto> GetPageAsync(
        string? category, string? search, bool missingOnly, int page, int pageSize,
        CancellationToken cancellationToken = default);

    Task SaveAsync(SaveDictionaryDto dto, CancellationToken cancellationToken = default);
}
