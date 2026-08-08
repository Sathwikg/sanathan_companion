using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>One language's rendering of a <see cref="TranslationTerm"/>.</summary>
/// <remarks>
/// Split from the term itself so the admin grid can be "one row, N language columns" and so a
/// harvest can ask "is this string already known?" with a single index probe, independent of
/// language.
/// </remarks>
public class TranslationTermText : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TermId { get; set; }
    public TranslationTerm? Term { get; set; }

    public Guid LanguageId { get; set; }
    public Language? Language { get; set; }

    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// True while the value is still the one shipped in the seed files. Re-importing only
    /// overwrites seed-owned rows, so an admin's edit is never clobbered by a deploy.
    /// </summary>
    public bool IsSeeded { get; set; }
}
