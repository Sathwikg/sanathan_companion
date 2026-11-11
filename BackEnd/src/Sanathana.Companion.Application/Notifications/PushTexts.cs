using System.Collections.Concurrent;
using Sanathana.Companion.Application.Common.Translation;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Application.Notifications;

/// <summary>
/// The wording of every push in one language, plus the dictionary that turns database values
/// (a tithi, a festival's name) into that language.
/// </summary>
public sealed class PushTexts
{
    private readonly IReadOnlyDictionary<string, string> _labels;
    private readonly IReadOnlyDictionary<string, string> _english;
    private readonly TranslationSnapshot? _snapshot;

    public PushTexts(string language, IReadOnlyDictionary<string, string> labels, IReadOnlyDictionary<string, string> english, TranslationSnapshot? snapshot)
    {
        Language = language;
        _labels = labels;
        _english = english;
        _snapshot = snapshot;
    }

    public string Language { get; }

    /// <summary>The label in this language, else in English, else the key itself (which shows up in a test send).</summary>
    public string Get(string key)
        => _labels.TryGetValue(key, out var text) && !string.IsNullOrWhiteSpace(text) ? text
         : _english.TryGetValue(key, out var en) && !string.IsNullOrWhiteSpace(en) ? en
         : key;

    public bool Has(string key) => _labels.ContainsKey(key) || _english.ContainsKey(key);

    /// <summary>
    /// The label with its {placeholders} filled. Names in braces rather than {0} because the people
    /// who edit these on the Language Configs screen should be able to see what each one stands for.
    /// </summary>
    public string Format(string key, IReadOnlyDictionary<string, string>? args = null)
    {
        var text = Get(key);
        if (args is null) return text;

        foreach (var (name, value) in args)
            text = text.Replace("{" + name + "}", value, StringComparison.Ordinal);

        return text;
    }

    /// <summary>A whole database value (a festival's name) in this language, or unchanged when the dictionary has no entry.</summary>
    public string Term(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        if (_snapshot is null) return value;
        return _snapshot.TryGetWholeValue(TermMatcher.NormaliseKey(value), out var whole) ? whole : value;
    }

    /// <summary>A Panchangam phrase ("Dashami upto 14:05") in this language, the way the Panchangam screen shows it.</summary>
    public string Phrase(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        if (_snapshot is null) return value;
        if (_snapshot.TryGetWholeValue(TermMatcher.NormaliseKey(value), out var whole)) return whole;

        var translated = _snapshot.MatcherFor("panchangam").Translate(value);
        return PanchangamPhrasing.Postpose(translated, _snapshot);
    }
}

public interface IPushTextProvider
{
    /// <summary>The texts for a language code. Unknown or blank codes get English.</summary>
    Task<PushTexts> GetAsync(string? language, CancellationToken cancellationToken = default);
}

/// <summary>
/// Keeps each language's push wording for a few minutes, so a run that notifies a thousand seekers
/// reads the labels once. A singleton: the provider itself is scoped, like the repositories it reads.
/// </summary>
public sealed class PushTextCache
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public ConcurrentDictionary<string, (DateTime LoadedUtc, IReadOnlyDictionary<string, string> Labels)> Entries { get; }
        = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Reads the "push" namespace of the language dictionary: the database rows an administrator can
/// edit on Language Configs, over the shipped seed files, over English.
/// </summary>
public sealed class PushTextProvider : IPushTextProvider
{
    public const string Namespace = "push";
    private const string English = "en";

    private readonly IUnitOfWork _uow;
    private readonly ILocalizationSeedSource _seed;
    private readonly ITranslationCatalog _catalog;
    private readonly PushTextCache _cache;

    public PushTextProvider(IUnitOfWork uow, ILocalizationSeedSource seed, ITranslationCatalog catalog, PushTextCache cache)
    {
        _uow = uow;
        _seed = seed;
        _catalog = catalog;
        _cache = cache;
    }

    public async Task<PushTexts> GetAsync(string? language, CancellationToken cancellationToken = default)
    {
        var code = string.IsNullOrWhiteSpace(language) ? English : language.Trim().ToLowerInvariant();

        var english = await LabelsAsync(English, cancellationToken);
        var labels = code == English ? english : await LabelsAsync(code, cancellationToken);
        var snapshot = code == English ? null : await _catalog.GetAsync(code, cancellationToken);

        return new PushTexts(code, labels, english, snapshot);
    }

    private async Task<IReadOnlyDictionary<string, string>> LabelsAsync(string code, CancellationToken cancellationToken)
    {
        if (_cache.Entries.TryGetValue(code, out var hit) && DateTime.UtcNow - hit.LoadedUtc < PushTextCache.Lifetime)
            return hit.Labels;

        var labels = new Dictionary<string, string>(StringComparer.Ordinal);

        // The shipped wording first, so a database that has not imported the seed files yet (a fresh
        // install, a test) still sends real sentences rather than keys.
        if (_seed.Load().TryGetValue(code, out var seeded))
            foreach (var (key, value) in seeded)
                if (key.StartsWith(Namespace + ".", StringComparison.Ordinal))
                    labels[key] = value;

        var language = (await _uow.Languages.GetAllOrderedAsync(cancellationToken))
            .FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));

        if (language is not null)
            foreach (var row in await _uow.Localization.GetResourcesAsync(language.Id, cancellationToken))
                if (row.Namespace == Namespace && !string.IsNullOrWhiteSpace(row.Value))
                    labels[row.Key] = row.Value;

        _cache.Entries[code] = (DateTime.UtcNow, labels);
        return labels;
    }
}
