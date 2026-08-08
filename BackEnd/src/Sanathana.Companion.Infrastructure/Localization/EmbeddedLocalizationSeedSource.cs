using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Infrastructure.Localization;

/// <summary>Serves the seed files embedded in the Infrastructure assembly.</summary>
public class EmbeddedLocalizationSeedSource : ILocalizationSeedSource
{
    public IReadOnlyDictionary<string, Dictionary<string, string>> Load() => LocalizationSeedFiles.Load();
}
