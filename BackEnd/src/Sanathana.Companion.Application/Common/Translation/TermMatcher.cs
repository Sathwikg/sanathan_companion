using System.Text;
using System.Text.RegularExpressions;

namespace Sanathana.Companion.Application.Common.Translation;

/// <summary>
/// Substitutes translated terms into English strings that come straight out of the database —
/// panchangam fragments such as "Navami upto 00:35, 22 Dec, Dasami from 00:36, 22 Dec" — while
/// leaving times, numbers, dates, punctuation and separators byte-identical.
/// </summary>
/// <remarks>
/// <para>Translation runs in three phases:</para>
/// <list type="number">
/// <item><description><b>Mask</b> — e-mail addresses and URLs are swapped for single sentinel
/// characters taken from the Unicode Private Use Area, and the original text is stashed. They are
/// masked because their interiors are full of letters separated by dots and slashes, so a term such
/// as "God" would otherwise match inside "www.god.com" and corrupt the address.</description></item>
/// <item><description><b>Replace</b> — one compiled regex, built from every term, rewrites all
/// matches in a single left-to-right scan.</description></item>
/// <item><description><b>Unmask</b> — the stashed literals go back where their sentinels sit.</description></item>
/// </list>
/// <para><b>Numbers, clock times and decimals are deliberately NOT masked.</b> Masking them used to
/// look safe and was in fact the source of three separate faults: a term containing a digit
/// ("Ekadasi 11", "Sunrise 06:00") could never match because the digits had already become a
/// sentinel; and the word-boundary lookarounds were being evaluated against the masked text, so
/// "22Dec" matched "Dec" and "None1" matched "None" even though a digit is supposed to block a
/// match. What actually keeps numbers intact is structural rather than positional: a term must
/// contain a letter (see <see cref="IsUsableTerm"/>), so no term can match inside a run of digits;
/// the lookarounds refuse a match glued to a letter or digit; and an alternative whose own text
/// starts or ends with a digit additionally refuses to sit against the "." or ":" of a decimal or a
/// clock time, so "Ekadasi 11" cannot bite the "11" out of "11:30".</para>
/// <para>An instance is immutable once constructed: every field is readonly, <see cref="Regex"/> is
/// thread-safe, and all per-call state is local. Build one per language and share it freely.</para>
/// </remarks>
public sealed partial class TermMatcher
{
    /// <summary>First code point of the Unicode Private Use Area block used for sentinels.</summary>
    private const char PrivateUseFirst = '\uE000';

    /// <summary>Last code point of that block.</summary>
    private const char PrivateUseLast = '\uF8FF';

    /// <summary>
    /// What a Private Use character in caller-supplied text is turned into. It has to be a real
    /// character rather than nothing at all: deleting it would close the gap and weld the two runs
    /// either side of it into one token, so "Uttara" + U+E000 + "shada" would be read as the term
    /// "Uttarashada" that the caller never wrote. U+FFFD is neither a letter nor a digit, so it acts
    /// as a word boundary and keeps the two runs apart.
    /// </summary>
    private const char PrivateUseReplacement = '\uFFFD';

    /// <summary>How many literals a single <see cref="Translate"/> call can stash (one sentinel each).</summary>
    private const int MaxMasks = PrivateUseLast - PrivateUseFirst + 1;

    /// <summary>
    /// Characters that may not sit immediately either side of a match. Letters and digits are the
    /// word-boundary rule proper; the Private Use range is there because a sentinel stands in for a
    /// URL or an e-mail address, which are word-like, so a term must not glue itself to one.
    /// </summary>
    private const string BoundaryClass = @"\p{L}\p{N}\uE000-\uF8FF";

    /// <summary>Normalised source term → translated text. Never mutated after construction.</summary>
    private readonly Dictionary<string, string> _translations;

    /// <summary>The single alternation over every usable term, or <see langword="null"/> when there is nothing to match.</summary>
    private readonly Regex? _termRegex;

    /// <summary>Cached evaluator so each <see cref="Translate"/> call does not allocate a delegate.</summary>
    private readonly MatchEvaluator _evaluator;

    /// <summary>
    /// Builds the engine for one language.
    /// </summary>
    /// <param name="terms">Source English term → translated text. Terms that cannot safely take part
    /// in matching are dropped here rather than at match time: anything shorter than two characters
    /// (a one-character term matches almost everywhere), and anything without a letter — purely
    /// numeric or purely punctuation terms would collide with the separators the output is required
    /// to preserve. Both halves of every entry are cleaned of Private Use characters here, once:
    /// a translation VALUE carrying one (legacy Indic font encodings such as Anu and Shree-Lipi put
    /// Telugu glyphs in that block, so this is real data, not a hypothetical) would otherwise be
    /// spliced into the text after masking and read back by phase C as a sentinel, either leaking a
    /// Private Use character to the caller or expanding into an unrelated stashed literal.</param>
    public TermMatcher(IReadOnlyDictionary<string, string> terms)
    {
        ArgumentNullException.ThrowIfNull(terms);

        _translations = new Dictionary<string, string>(StringComparer.Ordinal);
        List<string> sources = [];

        foreach (KeyValuePair<string, string> term in terms)
        {
            if (term.Key is null || term.Value is null) continue;

            // The key is neutralised the same way caller input is, so a term and the text it is
            // meant to match agree on what a stray Private Use character became.
            string source = NeutralisePrivateUse(term.Key).Trim();
            if (!IsUsableTerm(source)) continue;

            // The value is stripped rather than neutralised: it is emitted verbatim and never
            // re-scanned, so there is nothing to keep apart and no reason to show U+FFFD to a user.
            string translated = StripPrivateUse(term.Value).Trim();
            if (translated.Length == 0) continue;

            string key = Normalise(source);
            if (key.Length < 2) continue;

            // First definition wins, so a duplicate spelling cannot make the result depend on
            // dictionary iteration order.
            if (!_translations.TryAdd(key, translated)) continue;

            sources.Add(source);
        }

        _evaluator = ReplaceTerm;
        _termRegex = sources.Count == 0 ? null : BuildTermRegex(sources);
    }

    /// <summary>Number of terms that survived the build-time filter and can actually match.</summary>
    public int TermCount => _translations.Count;

    /// <summary>
    /// Returns <paramref name="value"/> with every known term replaced by its translation. Times,
    /// numbers, dates, punctuation and unknown words are returned exactly as they arrived, and the
    /// result never contains a Private Use Area character.
    /// </summary>
    public string Translate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value ?? string.Empty;

        // Sentinels are addressed by position, so any Private Use character already present in the
        // input would be read back as somebody else's literal. Neutralise them before anything else
        // — including before the "no terms" shortcut below, which is exactly the path on which the
        // "no Private Use character in the output" guarantee used to be skipped.
        string text = NeutralisePrivateUse(value);

        // Nothing to match — and with no terms there is deliberately no regex to run (see BuildTermRegex).
        if (_termRegex is null) return text;

        List<string> literals = [];
        string masked = MaskLiterals(text, literals);
        string replaced = _termRegex.Replace(masked, _evaluator);
        return RestoreLiterals(replaced, literals);
    }

    /// <summary>
    /// Compiles every term into ONE alternation. Three details here are load-bearing and must not be
    /// "simplified" away.
    /// </summary>
    /// <remarks>
    /// <para><b>1. Alternatives are sorted by descending NORMALISED length.</b> .NET alternation is
    /// leftmost-<i>first</i>, not longest-match: at a given position the first alternative that can
    /// match wins. With "(Uttara|Uttarashada)" the input "Uttarashada" matches "Uttara" and leaves a
    /// dangling "shada". Sorting longest-first makes the more specific term win. The sort key has to
    /// be the normalised length, not the raw length: <see cref="ToAlternative"/> collapses every run
    /// of whitespace inside a term to <c>\s+</c>, so a term written as "Sukla" + twenty spaces +
    /// "Paksha" has a raw length of 31 while consuming only 12 characters of input, and sorting on
    /// the raw length would let it shadow the strictly longer "Sukla Paksha Navami".</para>
    /// <para><b>2. Word boundaries are lookarounds, not <c>\b</c>.</b> <c>\b</c> is defined against
    /// <c>\w</c>, so a trailing <c>\b</c> after a term ending in punctuation — "Krishna (Bahula)" —
    /// would <i>require</i> a following word character and the term could never match at end of
    /// input. <c>(?&lt;![\p{L}\p{N}])…(?![\p{L}\p{N}])</c> instead asserts only that the match is not
    /// glued to a letter or digit, which is the property actually wanted. Because these lookarounds
    /// run against the masked text, the sentinel range is part of the class as well; and because
    /// numbers are no longer masked, a digit in the input is still a digit when the assertion is
    /// evaluated, which is what makes "22Dec" and "None1" correctly refuse to match.</para>
    /// <para><b>3. Alternatives that begin or end with a digit carry an extra guard</b> so that a
    /// term such as "Ekadasi 11" cannot consume the "11" of the clock time "11:30" — see
    /// <see cref="ToAlternative"/>.</para>
    /// <para>Replacement is a single <see cref="Regex.Replace(string, MatchEvaluator)"/>. Looping
    /// <see cref="string.Replace(string, string)"/> over the terms would re-scan its own output, so
    /// "God" → "దేవుడు" applied before "Goddess" would destroy "Goddess" and the result would depend
    /// on dictionary iteration order.</para>
    /// </remarks>
    private static Regex BuildTermRegex(List<string> sources)
    {
        string alternation = string.Join('|', sources
            .Select(static source => (Source: source, Length: Normalise(source).Length))
            .OrderByDescending(static entry => entry.Length)
            .ThenBy(static entry => entry.Source, StringComparer.Ordinal)
            .Select(static entry => ToAlternative(entry.Source)));

        return new Regex(
            "(?<![" + BoundaryClass + "])(?:" + alternation + ")(?![" + BoundaryClass + "])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }

    /// <summary>
    /// Turns one term into a regex alternative. The term is escaped — real terms contain regex
    /// metacharacters, e.g. "Krishna (Bahula)" — and each run of whitespace inside it becomes
    /// <c>\s+</c> so that a term stored with one space still matches text stored with two.
    /// </summary>
    /// <remarks>
    /// A term whose own text ends with a digit gets <c>(?![.:]\d)</c> after it, and one that starts
    /// with a digit gets <c>(?&lt;!\d[.:])</c> before it. The shared lookarounds already stop a term
    /// gluing itself to a digit; these stop it gluing itself to the separator inside a decimal or a
    /// clock time, which is the one way a term could still split a literal that has to survive
    /// byte-identical: without the guard, "Ekadasi 11" would match inside "Ekadasi 11:30".
    /// </remarks>
    private static string ToAlternative(string source)
    {
        string body = string.Join(@"\s+", source
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(Regex.Escape));

        if (char.IsAsciiDigit(source[0])) body = @"(?<!\d[.:])" + body;
        if (char.IsAsciiDigit(source[^1])) body += @"(?![.:]\d)";

        return body;
    }

    /// <summary>
    /// Looks the matched text up by its normalised key. The regex is case-insensitive and tolerant of
    /// whitespace, so the matched text is not necessarily identical to the dictionary key; an
    /// unmatched lookup returns the original text rather than dropping it.
    /// </summary>
    private string ReplaceTerm(Match match)
        => _translations.TryGetValue(Normalise(match.Value), out string? translated)
            ? translated
            : match.Value;

    /// <summary>
    /// Phase A. Replaces every literal that must survive translation with one sentinel character and
    /// stashes the original in <paramref name="literals"/>, indexed by sentinel.
    /// </summary>
    private static string MaskLiterals(string value, List<string> literals)
        => LiteralRegex().Replace(value, match =>
        {
            // Out of sentinels: leave the literal in place rather than corrupt the mapping.
            if (literals.Count >= MaxMasks) return match.Value;

            literals.Add(match.Value);
            return ((char)(PrivateUseFirst + literals.Count - 1)).ToString();
        });

    /// <summary>
    /// Phase C. Puts the stashed literals back. Any sentinel without a literal behind it is dropped,
    /// so a Private Use character can never reach the caller — that sweep runs even when nothing was
    /// stashed, because "nothing was stashed" is precisely the case where a stray Private Use
    /// character would otherwise sail through untouched.
    /// </summary>
    private static string RestoreLiterals(string value, List<string> literals)
    {
        int start = IndexOfPrivateUse(value);
        if (start < 0) return value;

        StringBuilder builder = new(value.Length + 16);
        builder.Append(value, 0, start);

        for (int index = start; index < value.Length; index++)
        {
            char character = value[index];
            if (character is >= PrivateUseFirst and <= PrivateUseLast)
            {
                int slot = character - PrivateUseFirst;
                if (slot < literals.Count) builder.Append(literals[slot]);
            }
            else
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    /// <summary>Index of the first Private Use character, or -1 when there is none.</summary>
    private static int IndexOfPrivateUse(string value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            if (value[index] is >= PrivateUseFirst and <= PrivateUseLast) return index;
        }

        return -1;
    }

    /// <summary>
    /// Replaces every Private Use character with <see cref="PrivateUseReplacement"/>, preserving both
    /// the length of the text and the token split the character was creating. Used on caller input
    /// and on dictionary keys.
    /// </summary>
    private static string NeutralisePrivateUse(string value)
    {
        int start = IndexOfPrivateUse(value);
        if (start < 0) return value;

        char[] buffer = value.ToCharArray();
        for (int index = start; index < buffer.Length; index++)
        {
            if (buffer[index] is >= PrivateUseFirst and <= PrivateUseLast)
            {
                buffer[index] = PrivateUseReplacement;
            }
        }

        return new string(buffer);
    }

    /// <summary>
    /// Deletes every Private Use character. Used on translation values, which are emitted as they
    /// are and never matched against, so there is no token to keep apart.
    /// </summary>
    private static string StripPrivateUse(string value)
    {
        int start = IndexOfPrivateUse(value);
        if (start < 0) return value;

        StringBuilder builder = new(value.Length);
        builder.Append(value, 0, start);

        for (int index = start; index < value.Length; index++)
        {
            char character = value[index];
            if (character is not (>= PrivateUseFirst and <= PrivateUseLast)) builder.Append(character);
        }

        return builder.ToString();
    }

    /// <summary>
    /// A term earns a place in the alternation only if it is at least two characters long and
    /// contains a letter. One-character terms match almost every string; digit-only and
    /// punctuation-only terms would fight with the separators the output has to preserve. A term
    /// that mixes letters and digits — "Ekadasi 11", "Sri108" — is usable and does match.
    /// </summary>
    private static bool IsUsableTerm(string? source)
        => !string.IsNullOrWhiteSpace(source)
           && source.Trim().Length >= 2
           && source.Any(char.IsLetter);

    /// <summary>
    /// Trim, collapse internal whitespace, lowercase — the shared key shape for lookups.
    /// Public because the harvester and the admin save path must produce byte-identical keys;
    /// if they ever diverge from this, a harvested term stops matching its own translation.
    /// </summary>
    public static string NormaliseKey(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : WhitespaceRegex().Replace(value.Trim(), " ").ToLowerInvariant();

    private static string Normalise(string value) => NormaliseKey(value);

    /// <summary>
    /// Everything phase B must not see: addresses, whose interiors are letters joined by dots and
    /// slashes and would otherwise match terms. URLs lead so that the e-mail alternative cannot bite
    /// a fragment out of the middle of one. Numbers, clock times and decimals are NOT here — see the
    /// class remarks for why masking them was worse than leaving them alone.
    /// </summary>
    [GeneratedRegex(
        """
        (?:https?|ftp)://[^\s<>"']+
        |www\.[^\s<>"']+
        |[\p{L}\p{N}._%+-]+@[\p{L}\p{N}.-]+\.\p{L}{2,}
        """,
        RegexOptions.IgnorePatternWhitespace | RegexOptions.CultureInvariant)]
    private static partial Regex LiteralRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
