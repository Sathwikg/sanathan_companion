using Sanathana.Companion.Application.Common.Translation;

namespace Sanathana.Companion.Application.Interfaces;

/// <summary>
/// Caches a ready-to-use <see cref="TranslationSnapshot"/> per language.
/// </summary>
/// <remarks>
/// Registered as a SINGLETON on purpose. Without the cache the result filter would query the
/// database on every request and the whole feature would be a performance regression; with it,
/// steady-state translation costs no I/O at all.
/// </remarks>
public interface ITranslationCatalog
{
    /// <summary>The snapshot for a language code, or null for English / unknown codes.</summary>
    Task<TranslationSnapshot?> GetAsync(string? languageCode, CancellationToken cancellationToken = default);

    /// <summary>Drops the cache so the next request rebuilds. Call after ANY translation save.</summary>
    void Invalidate();
}
