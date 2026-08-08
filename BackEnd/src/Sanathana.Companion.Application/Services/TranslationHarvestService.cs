using System.Text.RegularExpressions;
using Sanathana.Companion.Application.Common.Translation;
using Sanathana.Companion.Application.DTOs.Localization;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Application.Services;

/// <inheritdoc />
public partial class TranslationHarvestService : ITranslationHarvestService
{
    private readonly IUnitOfWork _uow;
    private readonly IVocabularyColumnReader _reader;
    private readonly ITranslationMissLog _misses;
    private readonly ITermSeedService _termSeed;
    private readonly ITranslationCatalog? _catalog;

    public TranslationHarvestService(
        IUnitOfWork uow,
        IVocabularyColumnReader reader,
        ITranslationMissLog misses,
        ITermSeedService termSeed,
        ITranslationCatalog? catalog = null)
    {
        _uow = uow;
        _reader = reader;
        _misses = misses;
        _termSeed = termSeed;
        _catalog = catalog;
    }

    /// <summary>
    /// Month abbreviations produced by the "dd MMM" part of a time. Dates deliberately stay in
    /// English, so harvesting these would only add permanent noise to the admin's worklist.
    /// </summary>
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "sept", "oct", "nov", "dec"
    };

    public async Task<HarvestResultDto> HarvestAsync(CancellationToken cancellationToken = default)
    {
        var known = await _uow.Localization.GetTermKeysAsync(cancellationToken);
        var result = new HarvestResultDto();
        var pending = new Dictionary<string, TranslationTerm>(StringComparer.Ordinal);

        // Words that already appear inside a known multi-word term. Splitting "full day" into
        // "full" and "day" would add two useless entries that the phrase already covers.
        var wordsInsidePhrases = known
            .Where(k => k.Contains(' '))
            .SelectMany(k => k.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var source in await _uow.Localization.GetTranslationSourcesAsync(cancellationToken))
        {
            var values = await _reader.ReadDistinctAsync(
                source.TableName, source.ColumnName, source.MaxDistinct, cancellationToken);

            var candidates = source.Mode == HarvestMode.Words
                ? values.SelectMany(SplitWords)
                : values.Select(v => v.Trim());

            var addedHere = 0;
            foreach (var candidate in candidates)
            {
                if (!IsUsable(candidate)) continue;

                var key = TermMatcher.NormaliseKey(candidate);
                if (key.Length == 0 || known.Contains(key) || pending.ContainsKey(key)) continue;
                if (StopWords.Contains(key)) continue;

                // Only for word-split sources: a bare word already covered by a phrase term.
                if (source.Mode == HarvestMode.Words && wordsInsidePhrases.Contains(key)) continue;

                pending[key] = new TranslationTerm
                {
                    Id = Guid.NewGuid(),
                    TermKey = key,
                    Source = candidate,
                    Category = source.Category,
                    Origin = TermOrigin.Harvested,
                    IsActive = true
                };
                addedHere++;
            }

            result.BySource.Add(new HarvestSourceResultDto
            {
                TableName = source.TableName,
                ColumnName = source.ColumnName,
                DistinctValues = values.Count,
                NewTerms = addedHere
            });
        }

        // Anything the running app could not translate — including strings that exist in no table,
        // such as the values computed live by /panchangam/compute.
        foreach (var miss in _misses.Drain())
        {
            if (!IsUsable(miss.Value)) continue;
            var key = TermMatcher.NormaliseKey(miss.Value);
            if (key.Length == 0 || known.Contains(key) || pending.ContainsKey(key)) continue;

            pending[key] = new TranslationTerm
            {
                Id = Guid.NewGuid(),
                TermKey = key,
                Source = miss.Value,
                Category = miss.Category ?? "general",
                Origin = TermOrigin.RuntimeMiss,
                MissCount = miss.Count,
                IsActive = true
            };
            result.FromRuntimeMisses++;
        }

        foreach (var term in pending.Values)
            await _uow.Localization.AddTermAsync(term, cancellationToken);

        result.Added = pending.Count;
        if (result.Added > 0)
        {
            await _uow.SaveChangesAsync(cancellationToken);

            // Apply any shipped translations to what we just discovered. Startup seeding runs
            // BEFORE a harvest, so without this a freshly deployed database would create the
            // terms and then leave them untranslated even though we ship the text for them.
            result.SeededTranslations = await _termSeed.ImportTermTranslationsAsync(cancellationToken);

            _catalog?.Invalidate();
        }
        return result;
    }

    /// <summary>
    /// Splits a composite value into candidate words, dropping everything that is not vocabulary:
    /// clock times, dates, numbers and punctuation. "Navami upto 00:35, 22 Dec" yields
    /// Navami / upto / Dec.
    /// </summary>
    private static IEnumerable<string> SplitWords(string value)
        => WordRegex().Matches(value).Select(m => m.Value.Trim()).Where(w => w.Length > 0);

    /// <summary>A term must contain a letter and be long enough not to match everywhere.</summary>
    private static bool IsUsable(string? candidate)
        => !string.IsNullOrWhiteSpace(candidate)
           && candidate.Trim().Length >= 2
           && candidate.Any(char.IsLetter);

    /// <summary>Runs of letters (any script) plus internal hyphens/apostrophes — never digits.</summary>
    [GeneratedRegex(@"\p{L}[\p{L}'\-]*", RegexOptions.CultureInvariant)]
    private static partial Regex WordRegex();
}
