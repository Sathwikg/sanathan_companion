using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Sanathana.Companion.Application.Common.Translation;

/// <summary>
/// Puts a translated Panchangam phrase into the word order of the target language.
/// </summary>
/// <remarks>
/// <para>
/// The calculator writes "Ekadasi upto 10:36, Dvadasi from 10:37", and <see cref="TermMatcher"/>
/// swaps the words one for one, which yields "ఏకాదశి వరకు 10:36, ద్వాదశి నుండి 10:37". English puts
/// its preposition before the time; Telugu, Hindi, Tamil and Kannada all put the equivalent
/// postposition after it, so the right order is "ఏకాదశి 10:36 వరకు, ద్వాదశి 10:37 నుండి". This moves
/// each connective behind the clock time it governs, day suffix included ("03:56, 06 Nov వరకు").
/// </para>
/// <para>
/// Only the two connectives move; digits, colons, commas and the day suffix stay byte-identical,
/// which is what the clients' PanchangamWindows parser relies on (it reads clock times and the
/// ", dd" after them, never words). "full day" has no time and is left alone.
/// </para>
/// </remarks>
public static class PanchangamPhrasing
{
    /// <summary>The term category whose composite values are re-ordered.</summary>
    public const string Category = "panchangam";

    private static readonly ConcurrentDictionary<(string Upto, string From), Regex> Patterns = new();

    public static bool AppliesTo(string? category)
        => string.Equals(category, Category, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Re-orders using the language's own words for "upto" and "from" as the snapshot translated
    /// them. English (or a language with no translation for either word) comes back untouched.
    /// </summary>
    public static string Postpose(string text, TranslationSnapshot snapshot)
    {
        if (!snapshot.TryGetWholeValue(TermMatcher.NormaliseKey("upto"), out var upto)) return text;
        if (!snapshot.TryGetWholeValue(TermMatcher.NormaliseKey("from"), out var from)) return text;
        return Postpose(text, upto, from);
    }

    /// <summary>
    /// "{name} {upto} {HH:mm}[, dd MMM]" becomes "{name} {HH:mm}[, dd MMM] {upto}", and the same
    /// for "from". The English words themselves are never moved.
    /// </summary>
    public static string Postpose(string text, string uptoWord, string fromWord)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        uptoWord = uptoWord.Trim();
        fromWord = fromWord.Trim();
        if (uptoWord.Length == 0 || fromWord.Length == 0) return text;
        if (IsEnglish(uptoWord) && IsEnglish(fromWord)) return text;

        var pattern = Patterns.GetOrAdd((uptoWord, fromWord), key => new Regex(
            // the connective, then the time it governs with an optional ", dd Mon" day suffix
            $@"(?<=^|\s)(?<kw>{Regex.Escape(key.Upto)}|{Regex.Escape(key.From)})\s+" +
            @"(?<time>[0-9]{1,2}:[0-9]{2}(?:,\s*[0-9]{1,2}\s+[^\s,]+)?)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant));

        return pattern.Replace(text, "${time} ${kw}");
    }

    private static bool IsEnglish(string word)
        => word.Equals("upto", StringComparison.OrdinalIgnoreCase) || word.Equals("from", StringComparison.OrdinalIgnoreCase);
}
