namespace Sanathana.Companion.Application.Interfaces;

/// <summary>
/// Supplies the translation seed files that ship with the build (implemented in Infrastructure,
/// which embeds them as assembly resources).
/// </summary>
public interface ILocalizationSeedSource
{
    /// <summary>Seed entries grouped by language code, e.g. "te" -> { "common.save": "…" }.</summary>
    IReadOnlyDictionary<string, Dictionary<string, string>> Load();
}
