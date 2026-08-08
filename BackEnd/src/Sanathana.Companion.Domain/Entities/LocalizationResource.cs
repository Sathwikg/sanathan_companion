using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>
/// One UI label in one language. The English row is the base text every other language
/// falls back to. Keys are namespaced ("common.save", "nav.masters"); the part before the
/// first dot is the <see cref="Namespace"/>, which is what the Language Configs screen groups by.
/// </summary>
public class LocalizationResource : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Namespaced key, e.g. "common.save".</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Denormalised prefix of <see cref="Key"/> so the editor can filter cheaply.</summary>
    public string Namespace { get; set; } = string.Empty;

    public Guid LanguageId { get; set; }
    public Language? Language { get; set; }

    /// <summary>The translated text. Placeholders such as {0} are preserved verbatim.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// True when the row came from the seed JSON files and has not been edited in the UI.
    /// Re-importing the files only overwrites rows that are still seed-owned, so hand edits survive.
    /// </summary>
    public bool IsSeeded { get; set; }
}
