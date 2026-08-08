using Sanathana.Companion.Application.Common.Translation;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Application.Services;

/// <summary>
/// Puts the shipped vocabulary into the dictionary so an admin opens the Dictionary tab to a
/// complete worklist rather than an empty grid.
/// </summary>
public class TermSeedService : ITermSeedService
{
    private readonly IUnitOfWork _uow;
    private readonly ITermVocabularySource _vocabulary;
    private readonly ITranslationCatalog? _catalog;

    public TermSeedService(IUnitOfWork uow, ITermVocabularySource vocabulary, ITranslationCatalog? catalog = null)
    {
        _uow = uow;
        _vocabulary = vocabulary;
        _catalog = catalog;
    }

    /// <summary>Adds any missing source terms. Idempotent — existing terms are left untouched.</summary>
    public async Task<int> SeedTermsAsync(CancellationToken cancellationToken = default)
    {
        var known = await _uow.Localization.GetTermKeysAsync(cancellationToken);
        var added = 0;

        foreach (var (source, category) in _vocabulary.Terms())
        {
            var key = TermMatcher.NormaliseKey(source);
            if (key.Length == 0 || !known.Add(key)) continue;

            await _uow.Localization.AddTermAsync(new TranslationTerm
            {
                Id = Guid.NewGuid(),
                TermKey = key,
                Source = source.Trim(),
                Category = category,
                Origin = TermOrigin.Seeded,
                IsActive = true
            }, cancellationToken);
            added++;
        }

        if (added > 0)
        {
            await _uow.SaveChangesAsync(cancellationToken);
            _catalog?.Invalidate();
        }
        return added;
    }

    /// <summary>
    /// Applies shipped translations for terms, without overwriting anything an admin edited.
    /// </summary>
    public async Task<int> ImportTermTranslationsAsync(CancellationToken cancellationToken = default)
    {
        var byLanguage = _vocabulary.Translations();
        if (byLanguage.Count == 0) return 0;

        var languages = await _uow.Languages.GetAllOrderedAsync(cancellationToken);
        var terms = await _uow.Localization.GetTermsTrackedAsync(cancellationToken);
        var termByKey = terms.ToDictionary(t => t.TermKey, StringComparer.Ordinal);
        var written = 0;

        foreach (var (code, entries) in byLanguage)
        {
            var language = languages.FirstOrDefault(l =>
                string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));
            if (language is null) continue;

            var existing = (await _uow.Localization.GetTermTextsTrackedAsync(language.Id, cancellationToken))
                .ToDictionary(x => x.TermId);

            foreach (var (source, text) in entries)
            {
                if (string.IsNullOrWhiteSpace(text)) continue;
                if (!termByKey.TryGetValue(TermMatcher.NormaliseKey(source), out var term)) continue;

                if (existing.TryGetValue(term.Id, out var row))
                {
                    // Never clobber a hand edit; only refresh rows still owned by the seed.
                    if (!row.IsSeeded || string.Equals(row.Text, text, StringComparison.Ordinal)) continue;
                    row.Text = text;
                    _uow.Localization.UpdateTermText(row);
                }
                else
                {
                    await _uow.Localization.AddTermTextAsync(new TranslationTermText
                    {
                        Id = Guid.NewGuid(),
                        TermId = term.Id,
                        LanguageId = language.Id,
                        Text = text,
                        IsSeeded = true
                    }, cancellationToken);
                }
                written++;
            }
        }

        if (written > 0)
        {
            await _uow.SaveChangesAsync(cancellationToken);
            _catalog?.Invalidate();
        }
        return written;
    }
}
