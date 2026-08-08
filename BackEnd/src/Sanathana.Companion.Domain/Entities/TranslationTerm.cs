using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>Where a term came from, which drives how the admin triages it.</summary>
public enum TermOrigin
{
    /// <summary>Shipped with the build (e.g. the Panchangam name tables).</summary>
    Seeded = 0,
    /// <summary>Found by scanning a registered database column.</summary>
    Harvested = 1,
    /// <summary>Seen at runtime on a translatable field with no translation available.</summary>
    RuntimeMiss = 2,
    /// <summary>Typed in by an administrator.</summary>
    Manual = 3
}

/// <summary>
/// One entry in the shared vocabulary: an English string that can appear in database content, in
/// any table, any number of times.
/// </summary>
/// <remarks>
/// This is the counterpart to <see cref="EntityTranslation"/>, and the two are complementary rather
/// than alternatives:
/// <list type="bullet">
/// <item><see cref="EntityTranslation"/> answers "what is deity 7f3a…'s name in Telugu?" — it is
/// keyed by row, and grows with the number of rows.</item>
/// <item><see cref="TranslationTerm"/> answers "what is the word <c>Navami</c> in Telugu?" — it is
/// keyed by the text itself, and grows only with the size of the vocabulary.</item>
/// </list>
/// The distinction is what makes Panchangam tractable: 1460 rows contain only ~59 distinct words
/// inside <c>TithiDetails</c>, because the rest of each value is clock times. Translating rows
/// would need tens of thousands of entries; translating terms needs about a hundred and fifty, and
/// covers every year generated in future for free.
/// </remarks>
public class TranslationTerm : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Normalised lookup key — trimmed, internal whitespace collapsed, lower-invariant.
    /// Computed by <see cref="Normalise"/> at every write so read and write can never diverge.
    /// </summary>
    public string TermKey { get; set; } = string.Empty;

    /// <summary>The English text exactly as it appears in the data; what the admin edits against.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Grouping bucket for the editor, e.g. "panchangam", "deity", "status".</summary>
    public string Category { get; set; } = "general";

    public TermOrigin Origin { get; set; } = TermOrigin.Harvested;

    /// <summary>
    /// How many times the translation layer saw this value and had nothing to substitute.
    /// Sorts the admin's worklist by what users are actually hitting.
    /// </summary>
    public int MissCount { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<TranslationTermText> Texts { get; set; } = new List<TranslationTermText>();

    /// <summary>
    /// The single definition of the lookup key. Both the matcher and every write path must use
    /// this — an inconsistency here silently stops terms from ever matching.
    /// </summary>
    public static string Normalise(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var trimmed = value.Trim();
        var sb = new System.Text.StringBuilder(trimmed.Length);
        var lastWasSpace = false;
        foreach (var ch in trimmed)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!lastWasSpace) sb.Append(' ');
                lastWasSpace = true;
            }
            else
            {
                sb.Append(char.ToLowerInvariant(ch));
                lastWasSpace = false;
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Terms that must never enter the matcher: a one-character term would match almost everywhere,
    /// and a purely numeric or punctuation term would corrupt times and dates.
    /// </summary>
    public static bool IsUsable(string? source)
    {
        var key = Normalise(source);
        return key.Length >= 2 && key.Any(char.IsLetter);
    }
}
