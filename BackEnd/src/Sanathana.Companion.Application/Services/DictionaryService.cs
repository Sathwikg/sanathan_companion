using Sanathana.Companion.Application.Common.Translation;
using Sanathana.Companion.Application.DTOs.Localization;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Exceptions;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Application.Services;

/// <summary>
/// The admin surface over the shared term dictionary — the vocabulary that translates database
/// text across every form.
/// </summary>
public class DictionaryService : IDictionaryService
{
    private const string BaseCode = "en";

    private readonly IUnitOfWork _uow;
    private readonly ITranslationCatalog? _catalog;

    public DictionaryService(IUnitOfWork uow, ITranslationCatalog? catalog = null)
    {
        _uow = uow;
        _catalog = catalog;
    }

    public async Task<DictionaryPageDto> GetPageAsync(
        string? category, string? search, bool missingOnly, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var languages = (await _uow.Languages.GetAllOrderedAsync(cancellationToken))
            .Where(l => l.IsActive)
            .OrderByDescending(IsBase)
            .ThenBy(l => l.Name)
            .ToList();

        var terms = await _uow.Localization.GetTermsTrackedAsync(cancellationToken);
        var targets = languages.Where(l => !IsBase(l)).ToList();

        var rows = terms
            .Where(t => t.IsActive)
            .Select(t =>
            {
                var row = new DictionaryRowDto
                {
                    TermId = t.Id,
                    Source = t.Source,
                    Category = t.Category,
                    Origin = t.Origin.ToString(),
                    MissCount = t.MissCount
                };
                foreach (var l in targets)
                    row.Values[l.LanguageId()] = t.Texts.FirstOrDefault(x => x.LanguageId == l.Id)?.Text ?? string.Empty;
                return row;
            })
            .ToList();

        var missingCount = rows.Count(r => r.Values.Values.Any(string.IsNullOrWhiteSpace));

        IEnumerable<DictionaryRowDto> q = rows;
        if (!string.IsNullOrWhiteSpace(category))
            q = q.Where(r => string.Equals(r.Category, category, StringComparison.OrdinalIgnoreCase));
        if (missingOnly)
            q = q.Where(r => r.Values.Values.Any(string.IsNullOrWhiteSpace));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(r => r.Source.Contains(s, StringComparison.OrdinalIgnoreCase)
                          || r.Values.Values.Any(v => v.Contains(s, StringComparison.OrdinalIgnoreCase)));
        }

        // Runtime misses first (users are actually hitting those), then alphabetical.
        var ordered = q.OrderByDescending(r => r.MissCount)
                       .ThenBy(r => r.Source, StringComparer.OrdinalIgnoreCase)
                       .ToList();

        pageSize = Math.Clamp(pageSize, 1, 200);
        var total = ordered.Count;
        var pageCount = total == 0 ? 1 : (int)Math.Ceiling(total / (double)pageSize);
        page = Math.Clamp(page, 1, pageCount);

        return new DictionaryPageDto
        {
            Languages = languages.Select(l => new MatrixLanguageDto
            {
                LanguageId = l.Id,
                Code = (l.Code ?? string.Empty).ToLowerInvariant(),
                Name = l.Name,
                NativeName = l.NativeName,
                IsBase = IsBase(l)
            }).ToList(),
            Categories = rows.Select(r => r.Category).Distinct(StringComparer.OrdinalIgnoreCase)
                             .OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList(),
            Rows = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
            MissingCount = missingCount
        };
    }

    public async Task SaveAsync(SaveDictionaryDto dto, CancellationToken cancellationToken = default)
    {
        var languages = (await _uow.Languages.GetAllOrderedAsync(cancellationToken)).ToDictionary(l => l.Id);
        var terms = (await _uow.Localization.GetTermsTrackedAsync(cancellationToken)).ToDictionary(t => t.Id);

        // Group by language so each language's rows are loaded once.
        var byLanguage = new Dictionary<Guid, List<(Guid TermId, string Value)>>();
        foreach (var row in dto.Rows)
        {
            if (!terms.ContainsKey(row.TermId))
                throw new BadRequestException($"Term '{row.TermId}' does not exist.");

            foreach (var (languageId, value) in row.Values)
            {
                if (!languages.TryGetValue(languageId, out var language))
                    throw new BadRequestException($"Language '{languageId}' was not found.");
                if (IsBase(language)) continue; // English is the source text

                if (!byLanguage.TryGetValue(languageId, out var list))
                    byLanguage[languageId] = list = [];
                list.Add((row.TermId, value ?? string.Empty));
            }
        }

        foreach (var (languageId, items) in byLanguage)
        {
            var existing = (await _uow.Localization.GetTermTextsTrackedAsync(languageId, cancellationToken))
                .ToDictionary(x => x.TermId);

            foreach (var (termId, raw) in items)
            {
                var value = raw.Trim();

                if (existing.TryGetValue(termId, out var row))
                {
                    if (value.Length == 0)
                    {
                        _uow.Localization.RemoveTermText(row);   // back to the English source text
                        existing.Remove(termId);
                    }
                    else if (!string.Equals(row.Text, value, StringComparison.Ordinal))
                    {
                        row.Text = value;
                        row.IsSeeded = false;                     // protect the edit from re-import
                        _uow.Localization.UpdateTermText(row);
                    }
                }
                else if (value.Length > 0)
                {
                    var added = new TranslationTermText
                    {
                        Id = Guid.NewGuid(),
                        TermId = termId,
                        LanguageId = languageId,
                        Text = value,
                        IsSeeded = false
                    };
                    await _uow.Localization.AddTermTextAsync(added, cancellationToken);
                    existing[termId] = added;
                }
            }
        }

        await _uow.SaveChangesAsync(cancellationToken);
        _catalog?.Invalidate();
    }

    private static bool IsBase(Language l) => string.Equals(l.Code, BaseCode, StringComparison.OrdinalIgnoreCase);
}

file static class LanguageExtensions
{
    /// <summary>Small readability helper so the projection above reads cleanly.</summary>
    public static Guid LanguageId(this Language l) => l.Id;
}
