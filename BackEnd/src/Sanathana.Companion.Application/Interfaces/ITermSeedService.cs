namespace Sanathana.Companion.Application.Interfaces;

public interface ITermSeedService
{
    /// <summary>Adds any shipped vocabulary terms that are not in the dictionary yet.</summary>
    Task<int> SeedTermsAsync(CancellationToken cancellationToken = default);

    /// <summary>Applies shipped term translations, leaving admin edits alone.</summary>
    Task<int> ImportTermTranslationsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Supplies the vocabulary that ships with the build (implemented in Infrastructure, which owns
/// both the Panchangam code tables and the embedded translation files).
/// </summary>
public interface ITermVocabularySource
{
    /// <summary>Source term + its category, e.g. ("Navami", "panchangam").</summary>
    IReadOnlyList<(string Source, string Category)> Terms();

    /// <summary>Language code -&gt; (source term -&gt; translated text).</summary>
    IReadOnlyDictionary<string, Dictionary<string, string>> Translations();
}
