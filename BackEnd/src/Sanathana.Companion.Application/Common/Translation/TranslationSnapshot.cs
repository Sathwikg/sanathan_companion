using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Application.Common.Translation;

/// <summary>
/// Everything needed to translate one response, for one language, with no database access.
/// Immutable and shared across requests — built once by <see cref="ITranslationCatalog"/> and
/// thrown away when an admin saves a translation.
/// </summary>
public sealed class TranslationSnapshot
{
    private readonly IReadOnlyDictionary<string, string> _entities;
    private readonly IReadOnlyDictionary<string, string> _wholeValues;
    private readonly IReadOnlyDictionary<string, TermMatcher> _matchersByCategory;
    private readonly TermMatcher _allCategories;
    private readonly IReadOnlySet<string> _disabledRoutes;

    public TranslationSnapshot(
        Guid languageId,
        string code,
        IReadOnlyDictionary<string, string> entities,
        IReadOnlyDictionary<string, string> wholeValues,
        IReadOnlyDictionary<string, TermMatcher> matchersByCategory,
        TermMatcher allCategories,
        IReadOnlySet<string> disabledRoutes)
    {
        LanguageId = languageId;
        Code = code;
        _entities = entities;
        _wholeValues = wholeValues;
        _matchersByCategory = matchersByCategory;
        _allCategories = allCategories;
        _disabledRoutes = disabledRoutes;
    }

    public Guid LanguageId { get; }
    public string Code { get; }

    /// <summary>True when there is genuinely nothing to translate, so the walk can be skipped.</summary>
    public bool IsEmpty => _entities.Count == 0 && _wholeValues.Count == 0 && _allCategories.TermCount == 0;

    /// <summary>Per-row override: "Deity:{id}:Name" -&gt; translated text.</summary>
    public bool TryGetEntity(string bundleKey, out string text) => _entities.TryGetValue(bundleKey, out text!);

    /// <summary>Exact match on the whole value — the fast path for controlled vocabulary.</summary>
    public bool TryGetWholeValue(string normalisedKey, out string text) => _wholeValues.TryGetValue(normalisedKey, out text!);

    /// <summary>The phrase matcher for a category, or the all-categories one when no category is given.</summary>
    public TermMatcher MatcherFor(string? category)
        => category is not null && _matchersByCategory.TryGetValue(category, out var m) ? m : _allCategories;

    /// <summary>
    /// False when an admin has opted this UI route out of the language on the Forms tab —
    /// the response is then left in English.
    /// </summary>
    public bool IsRouteTranslated(string? route)
    {
        if (_disabledRoutes.Count == 0 || string.IsNullOrWhiteSpace(route)) return true;
        return !_disabledRoutes.Contains(NormaliseRoute(route));
    }

    public static string NormaliseRoute(string route) => route.Trim().Trim('/').ToLowerInvariant();

    public static string EntityKey(string entityType, string entityKey, string field)
        => EntityTranslation.BundleKey(entityType, entityKey, field);
}
