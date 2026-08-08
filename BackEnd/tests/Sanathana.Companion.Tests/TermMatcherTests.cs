using System.Collections.Concurrent;
using Sanathana.Companion.Application.Common.Translation;

namespace Sanathana.Companion.Tests;

/// <summary>
/// Behavioural contract for <see cref="TermMatcher"/>.
/// </summary>
/// <remarks>
/// <para>Translations here are ASCII markers such as "[navami]" rather than real Telugu. The inputs
/// are the genuine production strings (they are what the engine has to survive); the outputs only
/// have to be recognisable, and keeping the file ASCII-only means no test can ever fail because of
/// how the source was encoded on disk. Where a non-ASCII character is part of the behaviour under
/// test -- the en dash in a Panchangam range, a Private Use character in hostile data -- it is written
/// as an escape so it is visible in review.</para>
/// </remarks>
public class TermMatcherTests
{
    private const char Pua = '\uE000';           // first sentinel code point
    private const char Pua5 = '\uE005';
    private const char Replacement = '\uFFFD';   // what caller-supplied PUA is turned into
    private const string EnDash = "\u2013";      // the separator PanchangamCalculator.Range emits

    /// <summary>The vocabulary the panchangam pipeline actually feeds this engine.</summary>
    private static readonly Dictionary<string, string> Panchangam = new(StringComparer.Ordinal)
    {
        ["Navami"] = "[navami]",
        ["Dasami"] = "[dasami]",
        ["Ekadasi"] = "[ekadasi]",
        ["Tadiya"] = "[tadiya]",
        ["Pournami"] = "[pournami]",
        ["Uttara"] = "[uttara]",
        ["Uttarashada"] = "[uttarashada]",
        ["Adhika"] = "[adhika]",
        ["Jyeshtham"] = "[jyeshtham]",
        ["Shukla"] = "[shukla]",
        ["Krishna (Bahula)"] = "[krishna-bahula]",
        ["upto"] = "[upto]",
        ["from"] = "[from]",
        ["full day"] = "[full day]",
        ["None"] = "[none]",
        ["Dec"] = "[dec]",
        ["Jul"] = "[jul]"
    };

    private static TermMatcher Production() => new(Panchangam);

    private static TermMatcher Of(params (string Source, string Translation)[] terms)
        => new(terms.ToDictionary(term => term.Source, term => term.Translation, StringComparer.Ordinal));

    private static bool HasPrivateUse(string value) => value.Any(c => c is >= '\uE000' and <= '\uF8FF');

    // ---------------------------------------------------------------- production strings

    [Fact]
    public void Translates_the_tithi_line_and_leaves_every_time_and_date_alone()
    {
        Assert.Equal(
            "[navami] [upto] 00:35, 22 [dec], [dasami] [from] 00:36, 22 [dec]",
            Production().Translate("Navami upto 00:35, 22 Dec, Dasami from 00:36, 22 Dec"));
    }

    [Theory]
    [InlineData("Dasami full day", "[dasami] [full day]")]
    [InlineData("Dasami upto 13:58", "[dasami] [upto] 13:58")]
    [InlineData("upto 01:41, 27 Jul", "[upto] 01:41, 27 [jul]")]
    [InlineData("Uttarashada upto 10:06", "[uttarashada] [upto] 10:06")]
    [InlineData("Adhika Jyeshtham", "[adhika] [jyeshtham]")]
    [InlineData("None", "[none]")]
    [InlineData("Krishna (Bahula)", "[krishna-bahula]")]
    [InlineData("Shukla", "[shukla]")]
    public void Translates_the_production_fragments(string input, string expected)
        => Assert.Equal(expected, Production().Translate(input));

    [Fact]
    public void Preserves_a_kalam_range_including_the_en_dash()
    {
        string input = $"06:11 {EnDash} 07:47, 13:20 {EnDash} 14:56";
        Assert.Equal(input, Production().Translate(input));
    }

    [Fact]
    public void Preserves_an_after_midnight_range_that_carries_a_date()
    {
        string input = $"23:41, 26 Jul {EnDash} 01:29, 27 Jul";
        Assert.Equal($"23:41, 26 [jul] {EnDash} 01:29, 27 [jul]", Production().Translate(input));
    }

    [Fact]
    public void A_term_ending_in_punctuation_matches_at_end_of_input()
        => Assert.Equal("[krishna-bahula]", Production().Translate("Krishna (Bahula)"));

    [Fact]
    public void Matching_is_case_insensitive_and_whitespace_tolerant()
    {
        Assert.Equal("[navami]", Production().Translate("NAVAMI"));
        Assert.Equal("[navami]", Production().Translate("navami"));
        Assert.Equal("[dasami] [full day]", Production().Translate("Dasami full   day"));
    }

    // ---------------------------------------------------------------- longest match first

    [Fact]
    public void Longest_term_wins_over_its_own_prefix()
    {
        Assert.Equal("[uttarashada]", Production().Translate("Uttarashada"));
        Assert.Equal("[uttara]", Production().Translate("Uttara"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Longest_term_wins_whatever_order_the_dictionary_is_built_in(bool longFirst)
    {
        TermMatcher matcher = longFirst
            ? Of(("Uttarashada", "LONG"), ("Uttara", "SHORT"))
            : Of(("Uttara", "SHORT"), ("Uttarashada", "LONG"));

        Assert.Equal("LONG", matcher.Translate("Uttarashada"));
        Assert.Equal("SHORT", matcher.Translate("Uttara"));
    }

    /// <summary>
    /// The re-scan trap: a loop of string.Replace would turn "Goddess" into "[god]dess". One regex
    /// pass over the input cannot see its own output, so both survive in either build order.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void God_does_not_eat_Goddess(bool godFirst)
    {
        TermMatcher matcher = godFirst
            ? Of(("God", "[god]"), ("Goddess", "[goddess]"))
            : Of(("Goddess", "[goddess]"), ("God", "[god]"));

        Assert.Equal("[goddess] and [god]", matcher.Translate("Goddess and God"));
        Assert.Equal("[goddess]", matcher.Translate("Goddess"));
    }

    [Fact]
    public void A_translation_is_never_re_scanned_even_when_it_contains_another_term()
    {
        // "[Navami]" is emitted for "Dasami"; if the output were re-scanned it would become "[[navami]]".
        TermMatcher matcher = Of(("Dasami", "Navami"), ("Navami", "[navami]"));
        Assert.Equal("Navami", matcher.Translate("Dasami"));
    }

    /// <summary>
    /// Regression: the alternatives used to be ordered by RAW source length, but ToAlternative
    /// collapses internal whitespace to \s+, so a term padded with twenty spaces (raw length 31)
    /// out-ranked the strictly longer "Sukla Paksha Navami" and left " Navami" dangling.
    /// </summary>
    [Fact]
    public void Ordering_uses_the_span_a_term_actually_consumes_not_its_raw_length()
    {
        TermMatcher matcher = Of(
            ("Sukla" + new string(' ', 20) + "Paksha", "SHORT-PADDED"),
            ("Sukla Paksha Navami", "LONG-CORRECT"));

        Assert.Equal(2, matcher.TermCount);
        Assert.Equal("LONG-CORRECT", matcher.Translate("Sukla Paksha Navami"));
        Assert.Equal("SHORT-PADDED", matcher.Translate("Sukla Paksha"));
    }

    // ---------------------------------------------------------------- lookaround boundaries

    [Theory]
    [InlineData("XNavamiY")]        // glued to letters
    [InlineData("Navamix")]
    [InlineData("xNavami")]
    [InlineData("Navami2")]         // glued to digits -- regression, masking used to hide these
    [InlineData("2Navami")]
    [InlineData("Navami22")]
    [InlineData("22Dec")]
    [InlineData("1None9")]
    [InlineData("None1")]
    [InlineData("1None")]
    public void A_term_glued_to_a_letter_or_a_digit_does_not_match(string input)
        => Assert.Equal(input, Production().Translate(input));

    [Theory]
    [InlineData("(Navami)", "([navami])")]
    [InlineData("Navami,", "[navami],")]
    [InlineData(".Navami.", ".[navami].")]
    [InlineData("Navami-Dasami", "[navami]-[dasami]")]
    [InlineData("Navami", "[navami]")]
    public void A_term_delimited_by_punctuation_or_the_ends_of_the_string_matches(string input, string expected)
        => Assert.Equal(expected, Production().Translate(input));

    [Fact]
    public void Digit_adjacency_does_not_depend_on_how_much_text_came_before_it()
    {
        // Regression: numbers used to be masked, and once the 6400-sentinel budget ran out the
        // masking stopped, so the SAME token gave two different answers.
        TermMatcher matcher = Production();
        string prefix = string.Concat(Enumerable.Repeat("7 ", 7000));

        Assert.Equal("Navami2", matcher.Translate("Navami2"));
        Assert.Equal(prefix + "Navami2", matcher.Translate(prefix + "Navami2"));
    }

    [Fact]
    public void Boundaries_hold_the_same_way_next_to_a_masked_address()
    {
        TermMatcher matcher = Of(("God", "[god]"));
        Assert.Equal("[god] at god@example.com", matcher.Translate("God at god@example.com"));
    }

    // ---------------------------------------------------------------- terms containing digits

    /// <summary>
    /// Regression: phase A used to rewrite every number to a sentinel before the term regex ran, so
    /// any term containing a digit was counted in TermCount and could never match.
    /// </summary>
    [Fact]
    public void A_term_containing_a_digit_matches_instead_of_losing_to_its_shorter_prefix()
    {
        TermMatcher matcher = Of(("Ekadasi", "<E>"), ("Ekadasi 11", "<E11>"), ("Dwadasi", "<D>"));

        Assert.Equal(3, matcher.TermCount);
        Assert.Equal("<E11> upto 05:00", matcher.Translate("Ekadasi 11 upto 05:00"));
        Assert.Equal("<E>", matcher.Translate("Ekadasi"));
    }

    [Fact]
    public void A_term_containing_a_clock_time_matches()
    {
        TermMatcher matcher = Of(("Sunrise", "<Sunrise>"), ("Sunrise 06:00", "<Sunrise0600>"));
        Assert.Equal("<Sunrise0600>", matcher.Translate("Sunrise 06:00"));
        Assert.Equal("<Sunrise>", matcher.Translate("Sunrise"));
    }

    [Fact]
    public void Every_term_that_TermCount_claims_is_live_really_can_match()
    {
        Dictionary<string, string> terms = new(StringComparer.Ordinal)
        {
            ["Ekadasi 11"] = "[ekadasi-11]",
            ["Sri108"] = "[sri108]",
            ["Navami"] = "[navami]"
        };
        TermMatcher matcher = new(terms);

        Assert.Equal(3, matcher.TermCount);
        foreach (KeyValuePair<string, string> term in terms)
        {
            Assert.Equal(term.Value, matcher.Translate(term.Key));
        }
    }

    /// <summary>A digit-bearing term must still not carve a piece out of a time or a decimal.</summary>
    [Theory]
    [InlineData("Ekadasi 11:30")]
    [InlineData("Ekadasi 11.5")]
    [InlineData("Ekadasi 111")]
    public void A_term_ending_in_a_digit_does_not_split_a_literal(string input)
        => Assert.Equal(input, Of(("Ekadasi 11", "<E11>")).Translate(input));

    [Fact]
    public void A_term_ending_in_a_digit_still_matches_when_the_next_character_is_a_separator()
        => Assert.Equal("<E11>, 22 Dec", Of(("Ekadasi 11", "<E11>")).Translate("Ekadasi 11, 22 Dec"));

    // ---------------------------------------------------------------- Private Use Area handling

    /// <summary>
    /// Regression: translation values were never sanitised, so a Private Use character arriving from
    /// the dictionary (legacy Indic font encodings put glyphs in that block) was read back by phase C
    /// as a sentinel and expanded into an unrelated stashed literal.
    /// </summary>
    [Fact]
    public void A_private_use_character_in_a_translation_value_does_not_inject_a_stashed_literal()
    {
        TermMatcher matcher = Of(("Navami", $"X{Pua}Y"), ("upto", "[upto]"));
        string result = matcher.Translate("Navami upto 00:35");

        Assert.Equal("XY [upto] 00:35", result);
        Assert.False(HasPrivateUse(result));
    }

    [Fact]
    public void A_private_use_character_in_a_translation_value_never_reaches_the_caller()
    {
        // No maskable literal anywhere in the input: the old code returned early and leaked the char.
        TermMatcher matcher = Of(("Tadiya", $"{Pua}[tadiya]"), ("Navami", "[navami]"));
        string result = matcher.Translate("Tadiya full day");

        Assert.Equal("[tadiya] full day", result);
        Assert.False(HasPrivateUse(result));
    }

    [Fact]
    public void Two_poisoned_translation_values_do_not_duplicate_or_drop_text()
    {
        TermMatcher matcher = Of(("Navami", $"{Pua}A"), ("Dasami", $"{Pua5}B"));
        Assert.Equal("A 5 B", matcher.Translate("Navami 5 Dasami"));
    }

    [Fact]
    public void A_value_made_only_of_private_use_characters_drops_the_term()
    {
        TermMatcher matcher = Of(("Navami", $"{Pua}{Pua5}"), ("Dasami", "[dasami]"));
        Assert.Equal(1, matcher.TermCount);
        Assert.Equal("Navami [dasami]", matcher.Translate("Navami Dasami"));
    }

    /// <summary>
    /// Regression: <c>Translate</c> returned early when there was no regex to run, above the point
    /// where input was sanitised, so the no-PUA guarantee was skipped on exactly that path.
    /// </summary>
    [Theory]
    [InlineData(0)]   // empty dictionary
    [InlineData(1)]   // non-empty, but every term filtered out
    public void A_private_use_character_in_the_input_never_survives_an_inert_dictionary(int shape)
    {
        TermMatcher matcher = shape == 0
            ? new TermMatcher(new Dictionary<string, string>())
            : Of(("a", "x"), ("22", "y"));

        Assert.Equal(0, matcher.TermCount);

        string result = matcher.Translate($"Uttara{Pua5}shada");
        Assert.False(HasPrivateUse(result));
        Assert.Equal($"Uttara{Replacement}shada", result);
    }

    /// <summary>
    /// Regression: stripping a Private Use character by deletion closed the gap and welded the runs
    /// either side of it into a token the caller never wrote, so "Uttara?shada" matched the
    /// eleven-character term "Uttarashada".
    /// </summary>
    [Fact]
    public void A_private_use_character_in_the_input_does_not_weld_the_tokens_around_it()
    {
        string result = Production().Translate($"Uttara{Pua}shada upto 10:06");

        Assert.False(HasPrivateUse(result));
        Assert.DoesNotContain("[uttarashada]", result);
        Assert.Equal($"[uttara]{Replacement}shada [upto] 10:06", result);
    }

    [Fact]
    public void Two_words_separated_by_a_private_use_character_are_still_two_words()
    {
        string result = Production().Translate($"Adhika{Pua}Jyeshtham");
        Assert.Equal($"[adhika]{Replacement}[jyeshtham]", result);
    }

    [Fact]
    public void No_input_ever_produces_a_private_use_character_in_the_output()
    {
        TermMatcher poisoned = Of(("Navami", $"{Pua}[navami]{Pua5}"), ("upto", "[upto]"));
        string[] inputs =
        {
            "Navami upto 00:35, 22 Dec",
            $"{Pua}",
            $"{Pua}Navami{Pua5}",
            $"Navami{Pua}upto{Pua5}00:35",
            "Tadiya full day",
            $"{Pua5}{Pua}{Pua5}"
        };

        foreach (TermMatcher matcher in new[] { Production(), poisoned })
        {
            foreach (string input in inputs)
            {
                Assert.False(HasPrivateUse(matcher.Translate(input)), $"leaked for: {input}");
            }
        }
    }

    // ---------------------------------------------------------------- masked addresses

    [Fact]
    public void A_term_inside_a_url_is_left_alone()
    {
        TermMatcher matcher = Of(("God", "[god]"));
        Assert.Equal(
            "See https://example.com/God/page for [god]",
            matcher.Translate("See https://example.com/God/page for God"));
    }

    [Theory]
    [InlineData("www.god.com", "www.god.com")]
    [InlineData("god@god.com", "god@god.com")]
    [InlineData("God www.god.com God", "[god] www.god.com [god]")]
    public void Addresses_survive_translation_byte_identical(string input, string expected)
        => Assert.Equal(expected, Of(("God", "[god]")).Translate(input));

    // ---------------------------------------------------------------- construction rules

    [Fact]
    public void Null_dictionary_is_rejected()
        => Assert.Throws<ArgumentNullException>(() => new TermMatcher(null!));

    [Fact]
    public void Empty_dictionary_returns_the_input_untouched()
    {
        TermMatcher matcher = new(new Dictionary<string, string>());

        Assert.Equal(0, matcher.TermCount);
        Assert.Equal(
            "Navami upto 00:35, 22 Dec",
            matcher.Translate("Navami upto 00:35, 22 Dec"));
        Assert.Equal(string.Empty, matcher.Translate(null));
        Assert.Equal(string.Empty, matcher.Translate(string.Empty));
        Assert.Equal("   ", matcher.Translate("   "));
    }

    [Fact]
    public void Blank_input_is_returned_as_it_arrived()
    {
        TermMatcher matcher = Production();
        Assert.Equal(string.Empty, matcher.Translate(null));
        Assert.Equal(string.Empty, matcher.Translate(string.Empty));
        Assert.Equal("  \t ", matcher.Translate("  \t "));
    }

    [Theory]
    [InlineData("a")]        // shorter than two characters
    [InlineData("22")]       // no letter
    [InlineData("--")]       // no letter
    [InlineData("  ")]       // blank
    public void Unusable_terms_are_dropped_at_build_time(string source)
    {
        TermMatcher matcher = Of((source, "[x]"), ("Navami", "[navami]"));
        Assert.Equal(1, matcher.TermCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_translation_drops_the_term(string translation)
    {
        TermMatcher matcher = Of(("Navami", translation), ("Dasami", "[dasami]"));
        Assert.Equal(1, matcher.TermCount);
        Assert.Equal("Navami [dasami]", matcher.Translate("Navami Dasami"));
    }

    [Fact]
    public void Duplicate_spellings_resolve_to_the_first_definition()
    {
        TermMatcher matcher = Of(("Navami", "FIRST"), ("navami", "SECOND"), ("  NAVAMI  ", "THIRD"));
        Assert.Equal(1, matcher.TermCount);
        Assert.Equal("FIRST", matcher.Translate("Navami"));
    }

    [Fact]
    public void TermCount_counts_only_the_terms_that_survived()
        => Assert.Equal(Panchangam.Count, Production().TermCount);

    // ---------------------------------------------------------------- thread safety

    [Fact]
    public void One_instance_serves_many_threads_with_identical_results()
    {
        TermMatcher matcher = Production();
        const string Input = "Navami upto 00:35, 22 Dec, Dasami from 00:36, 22 Dec";
        string expected = matcher.Translate(Input);

        ConcurrentBag<string> results = [];
        Parallel.For(0, 2000, new ParallelOptions { MaxDegreeOfParallelism = 16 }, _ =>
        {
            results.Add(matcher.Translate(Input));
            // A poisoned input on a neighbouring thread must not disturb the clean one: all
            // per-call state (the literal stash) has to be local.
            matcher.Translate($"Uttara{Pua}shada upto 10:06, www.god.com");
        });

        Assert.Equal(2000, results.Count);
        Assert.All(results, result => Assert.Equal(expected, result));
    }

    [Fact]
    public void Concurrent_construction_and_translation_stay_consistent()
    {
        string[] outputs = new string[500];
        Parallel.For(0, outputs.Length, index =>
        {
            TermMatcher matcher = new(Panchangam);
            outputs[index] = matcher.Translate("Uttarashada upto 10:06, 27 Jul");
        });

        Assert.All(outputs, output => Assert.Equal("[uttarashada] [upto] 10:06, 27 [jul]", output));
    }
}
