using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sanathana.Companion.Application.Common.Translation;
using Sanathana.Companion.Infrastructure.Localization;

namespace Sanathana.Companion.Tests;

/// <summary>
/// The two ways the database-text translation feature can quietly corrupt data, and the one way it
/// can quietly ship broken: (1) <c>[Translatable]</c> landing on a DTO it must never touch, and
/// (2) the embedded term dictionary drifting away from the vocabulary the Panchangam generator emits.
/// </summary>
/// <remarks>
/// <para>These are guard-rail tests, not example tests. Translation is opt-in — a property is only
/// ever rewritten because somebody typed the attribute on it — so the risk is not a bug in the
/// engine, it is an attribute added in good faith to the wrong DTO. Two placements are unsafe:
/// personal data (a user's own name coming back as a dictionary hit), and any DTO an editor posts
/// straight back to the server, where the translated text is written into the database and orphans
/// the row. See the class remarks on <see cref="TranslatableAttribute"/>.</para>
/// <para>The reflection tests scan the whole Application assembly rather than a hand-listed set of
/// types, so a DTO added next year is covered the moment it is compiled.</para>
/// <para>The term-file tests reuse the script-range approach of
/// <see cref="LocalizationSeedFilesTests"/>, applied to the other embedded family:
/// <c>Localization/Terms/terms.{code}.json</c>, which translates database text rather than UI labels.
/// Coverage is measured with <see cref="TermMatcher.NormaliseKey"/> because that — not the raw JSON
/// key — is what <c>TermSeedService.ImportTermTranslationsAsync</c> matches on in production.</para>
/// </remarks>
public class TranslationSafetyTests
{
    /// <summary>Unicode block each language must be written in, and must NOT stray outside.</summary>
    private static readonly Dictionary<string, (int Lo, int Hi, string Script)> Blocks = new()
    {
        ["te"] = (0x0C00, 0x0C7F, "Telugu"),
        ["hi"] = (0x0900, 0x097F, "Devanagari"),
        ["ta"] = (0x0B80, 0x0BFF, "Tamil"),
        ["kn"] = (0x0C80, 0x0CFF, "Kannada"),
    };

    /// <summary>Manifest-resource prefix the embedded term files live under.</summary>
    private const string TermResourcePrefix = "Sanathana.Companion.Infrastructure.Localization.Terms.";

    private static readonly Regex Latin = new("[A-Za-z]", RegexOptions.Compiled);

    // ------------------------------------------------------------------ reflection helpers

    /// <summary>Every type in the Application assembly, tolerating a partially loadable assembly.</summary>
    private static IReadOnlyList<Type> ApplicationTypes()
    {
        var assembly = typeof(TranslatableAttribute).Assembly;
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null).Select(t => t!).ToList();
        }
    }

    /// <summary>
    /// Properties carrying <c>[Translatable]</c>. Inherited properties are included, because the
    /// walker reads the runtime type and a base class annotation is just as live as a local one.
    /// </summary>
    private static IEnumerable<PropertyInfo> Annotated(Type type, bool declaredOnly = false)
    {
        var flags = BindingFlags.Public | BindingFlags.Instance;
        if (declaredOnly) flags |= BindingFlags.DeclaredOnly;

        return type.GetProperties(flags)
            .Where(p => p.GetIndexParameters().Length == 0)
            .Where(p => p.GetCustomAttribute<TranslatableAttribute>() is not null);
    }

    private static string Describe(IEnumerable<(Type Type, PropertyInfo Property)> hits) =>
        string.Join(", ", hits.Select(h => $"{h.Type.Name}.{h.Property.Name}"));

    // ------------------------------------------------------------------ A) personal data guard

    /// <summary>
    /// Names, e-mail addresses and free-text feedback are the user's own words. Running them through
    /// a shared dictionary would show one user another user's text whenever a name happens to collide
    /// with a term, and would make the same row read differently per language.
    /// </summary>
    [Fact]
    public void No_personal_data_DTO_is_ever_marked_translatable()
    {
        string[] forbidden = ["DTOs.Users", "DTOs.Auth", "DTOs.Feedback"];

        var offenders = ApplicationTypes()
            .Where(t => t.Namespace is { } ns && forbidden.Any(f => ns.Contains(f, StringComparison.Ordinal)))
            .SelectMany(t => Annotated(t).Select(p => (Type: t, Property: p)))
            .ToList();

        Assert.True(offenders.Count == 0,
            "Personal data must never be translated: user names, credentials and feedback text are the " +
            "user's own words, not controlled vocabulary, so a dictionary hit would silently replace " +
            $"one person's data with another's. Remove [Translatable] from: {Describe(offenders)}");
    }

    /// <summary>
    /// The namespace filter above is only a guard if it actually matches something. If the Users /
    /// Auth / Feedback DTOs are ever renamed or moved, the guard would pass vacuously.
    /// </summary>
    [Fact]
    public void The_personal_data_namespaces_still_exist()
    {
        string[] forbidden = ["DTOs.Users", "DTOs.Auth", "DTOs.Feedback"];
        var types = ApplicationTypes();

        foreach (var ns in forbidden)
        {
            Assert.True(
                types.Any(t => t.Namespace is { } n && n.Contains(ns, StringComparison.Ordinal)),
                $"No type was found under a namespace containing '{ns}', so the personal-data guard " +
                "is scanning nothing. Update the namespace list in this test.");
        }
    }

    // ------------------------------------------------------------------ B) round-trip guard

    /// <summary>
    /// Edit screens load one of these, bind it to inputs, and post it straight back. Several pickers
    /// use a display NAME as its own identifier, so a translated value arrives at the server as the
    /// value the user "chose" and is written to the database, orphaning the row.
    /// </summary>
    [Fact]
    public void No_write_back_DTO_is_ever_marked_translatable()
    {
        string[] prefixes = ["Create", "Update", "Save"];

        var offenders = ApplicationTypes()
            .Where(IsWriteBackShape)
            .SelectMany(t => Annotated(t).Select(p => (Type: t, Property: p)))
            .ToList();

        Assert.True(offenders.Count == 0,
            "Edit screens post these DTOs back unchanged, so a translated value would be saved into " +
            "the database as if the user had typed it — the row is then orphaned because pickers use " +
            "the display name as the identifier. Annotate read/list DTOs only. Offending: " +
            $"{Describe(offenders)}");

        bool IsWriteBackShape(Type t) =>
            t.Name.EndsWith("FormOptionsDto", StringComparison.Ordinal) ||
            prefixes.Any(p => t.Name.StartsWith(p, StringComparison.Ordinal));
    }

    /// <summary>Same vacuous-pass risk as the namespace guard: the name patterns must match real types.</summary>
    [Fact]
    public void The_write_back_name_patterns_still_match_real_DTOs()
    {
        var names = ApplicationTypes().Select(t => t.Name).ToList();

        Assert.Contains(names, n => n.EndsWith("FormOptionsDto", StringComparison.Ordinal));

        foreach (var prefix in new[] { "Create", "Update", "Save" })
        {
            Assert.True(names.Any(n => n.StartsWith(prefix, StringComparison.Ordinal)),
                $"No type starts with '{prefix}', so the round-trip guard is scanning nothing. " +
                "Update the prefix list in this test.");
        }
    }

    // ------------------------------------------------------------------ C) supported shapes only

    /// <summary>
    /// <c>TypeMapCache.Build</c> takes a <see cref="string"/> property or an
    /// <see cref="IList{T}"/> of string and IGNORES the attribute on anything else — no error, no
    /// log. An attribute on an <c>int</c>, an enum or a nested DTO therefore looks correct in source
    /// and does nothing at runtime.
    /// </summary>
    [Fact]
    public void Translatable_is_only_applied_to_string_or_string_list_properties()
    {
        var offenders = ApplicationTypes()
            .SelectMany(t => Annotated(t, declaredOnly: true).Select(p => (Type: t, Property: p)))
            .Where(h => h.Property.PropertyType != typeof(string)
                        && !typeof(IList<string>).IsAssignableFrom(h.Property.PropertyType))
            .ToList();

        Assert.True(offenders.Count == 0,
            "[Translatable] is silently ignored on any property that is not a string or an " +
            "IList<string>, so these annotations do nothing at all and the value ships in English: " +
            $"{Describe(offenders)}");
    }

    /// <summary>
    /// A translated string is written back onto the DTO through a compiled setter. Without a setter
    /// the walker skips the property, so a get-only translatable string is another silent no-op.
    /// (String lists are exempt: they are mutated in place and need no setter.)
    /// </summary>
    [Fact]
    public void A_translatable_string_property_is_readable_and_writable()
    {
        var offenders = ApplicationTypes()
            .SelectMany(t => Annotated(t, declaredOnly: true).Select(p => (Type: t, Property: p)))
            .Where(h => h.Property.PropertyType == typeof(string))
            .Where(h => !h.Property.CanRead || !h.Property.CanWrite)
            .ToList();

        Assert.True(offenders.Count == 0,
            "A translatable string needs a getter and a setter — the walker writes the translation " +
            $"back through the setter and skips the property without one: {Describe(offenders)}");
    }

    /// <summary>
    /// The two-argument attribute names a SIBLING property holding the row's primary key. If that
    /// name does not resolve, <c>CompileKeyGetter</c> returns null and the per-row
    /// <c>EntityTranslation</c> override is skipped entirely — the value quietly falls back to the
    /// shared dictionary, which is exactly the tier that must not win for per-row data.
    /// </summary>
    [Fact]
    public void Every_entity_backed_annotation_names_a_real_key_property()
    {
        var broken = new List<string>();
        var checkedCount = 0;

        foreach (var type in ApplicationTypes())
        {
            foreach (var property in Annotated(type, declaredOnly: true))
            {
                var attr = property.GetCustomAttribute<TranslatableAttribute>()!;

                // EntityType and KeyProperty come from the same constructor, so they travel together.
                if (attr.EntityType is null && attr.KeyProperty is null) continue;
                checkedCount++;

                if (string.IsNullOrWhiteSpace(attr.EntityType))
                {
                    broken.Add($"{type.Name}.{property.Name} has a key property but no entity type");
                    continue;
                }

                var key = attr.KeyProperty is null
                    ? null
                    : type.GetProperty(attr.KeyProperty, BindingFlags.Public | BindingFlags.Instance);

                if (key is null || !key.CanRead)
                    broken.Add($"{type.Name}.{property.Name} points at missing key property '{attr.KeyProperty}'");
            }
        }

        Assert.True(checkedCount > 0,
            "No entity-backed [Translatable(entityType, keyProperty)] annotation was found, so this " +
            "guard is scanning nothing — PanchangamDto.RegionName should be one.");

        Assert.True(broken.Count == 0,
            "An unresolvable key property makes the per-row EntityTranslation override silently " +
            $"disappear, leaving the shared dictionary to answer for a row-specific value: {string.Join(", ", broken)}");
    }

    // ------------------------------------------------------------------ D) term-file parity

    private static IReadOnlyDictionary<string, Dictionary<string, string>> ShippedTerms()
        => new EmbeddedTermVocabularySource().Translations();

    [Fact]
    public void Every_supported_language_ships_a_term_file()
    {
        var shipped = ShippedTerms();

        foreach (var code in Blocks.Keys)
        {
            Assert.True(shipped.ContainsKey(code),
                $"No embedded terms.{code}.json was found. Either the file is missing or the .csproj " +
                "stopped embedding Localization\\Terms\\**\\*.json with WithCulture=\"false\".");
            Assert.NotEmpty(shipped[code]);
        }
    }

    /// <summary>
    /// The source vocabulary is derived from the Panchangam code tables, so adding a nakshatra or a
    /// samvatsara there immediately creates a term with no translation. Every stored Panchangam row
    /// is built from these words, so one gap shows up as an English word inside a Telugu line.
    /// </summary>
    [Fact]
    public void Every_language_covers_every_panchangam_source_term()
    {
        var shipped = ShippedTerms();
        var expected = PanchangamTermSeed.All();

        foreach (var code in Blocks.Keys)
        {
            // Production matches on the normalised key, not on the raw JSON key.
            var covered = shipped[code].Keys
                .Select(TermMatcher.NormaliseKey)
                .ToHashSet(StringComparer.Ordinal);

            var missing = expected
                .Where(term => !covered.Contains(TermMatcher.NormaliseKey(term)))
                .ToList();

            Assert.True(missing.Count == 0,
                $"terms.{code}.json is missing {missing.Count} of the {expected.Count} terms the " +
                $"Panchangam generator can emit: {string.Join(", ", missing.Take(10))}");
        }
    }

    [Fact]
    public void No_shipped_term_translation_is_blank()
    {
        foreach (var (code, entries) in ShippedTerms())
        {
            var blank = entries.Where(e => string.IsNullOrWhiteSpace(e.Value)).Select(e => e.Key).ToList();
            Assert.True(blank.Count == 0,
                $"terms.{code}.json has {blank.Count} blank value(s), which TermSeedService skips, " +
                $"leaving the English text on screen: {string.Join(", ", blank.Take(10))}");
        }
    }

    /// <summary>
    /// A value still containing Latin letters is an untranslated (or half-translated) entry that
    /// looks translated to every other check — it has a key, it is non-blank, it imports cleanly.
    /// </summary>
    [Fact]
    public void No_shipped_term_translation_contains_latin_letters()
    {
        foreach (var (code, entries) in ShippedTerms())
        {
            var latin = entries
                .Where(e => Latin.IsMatch(e.Value))
                .Select(e => $"{e.Key}=\"{e.Value}\"")
                .ToList();

            Assert.True(latin.Count == 0,
                $"terms.{code}.json left {latin.Count} value(s) carrying Latin letters, i.e. still " +
                $"English: {string.Join(", ", latin.Take(10))}");
        }
    }

    [Fact]
    public void Every_shipped_term_translation_is_written_in_its_own_script()
    {
        var shipped = ShippedTerms();

        foreach (var (code, (lo, hi, script)) in Blocks)
        {
            foreach (var (key, value) in shipped[code])
            {
                Assert.True(value.Any(ch => ch >= lo && ch <= hi),
                    $"terms.{code}.json key '{key}' contains no {script} characters: \"{value}\".");
            }
        }
    }

    /// <summary>
    /// Telugu (U+0C00–U+0C7F) and Kannada (U+0C80–U+0CFF) are adjacent blocks that look alike, and
    /// Tamil is easy to confuse with both, so a copy-paste between sibling files is the likeliest
    /// defect — and the one hardest to spot by eye in review.
    /// </summary>
    [Fact]
    public void No_shipped_term_file_leaks_a_sibling_script()
    {
        var shipped = ShippedTerms();

        foreach (var code in Blocks.Keys)
        {
            foreach (var (otherCode, (lo, hi, script)) in Blocks.Where(b => b.Key != code))
            {
                foreach (var (key, value) in shipped[code])
                {
                    var offending = value.FirstOrDefault(ch => ch >= lo && ch <= hi);
                    Assert.True(offending == '\0',
                        $"terms.{code}.json key '{key}' contains {script} (from terms.{otherCode}.json) " +
                        $"character U+{(int)offending:X4} in \"{value}\".");
                }
            }
        }
    }

    /// <summary>
    /// End of the production path: JSON coverage is worthless if the term is then dropped by
    /// <see cref="TermMatcher"/>'s build-time filter or lost to a normalisation mismatch. This asserts
    /// the shipped dictionary actually translates every word the generator can emit.
    /// </summary>
    [Fact]
    public void The_shipped_dictionary_actually_translates_every_panchangam_term()
    {
        var shipped = ShippedTerms();
        var expected = PanchangamTermSeed.All();

        foreach (var (code, (lo, hi, script)) in Blocks)
        {
            var matcher = new TermMatcher(shipped[code]);

            var untranslated = expected
                .Where(term =>
                {
                    var result = matcher.Translate(term);
                    return string.Equals(result, term, StringComparison.Ordinal)
                           || !result.Any(ch => ch >= lo && ch <= hi);
                })
                .ToList();

            Assert.True(untranslated.Count == 0,
                $"The shipped '{code}' dictionary returns no {script} text for {untranslated.Count} " +
                $"term(s), so they reach the user in English: {string.Join(", ", untranslated.Take(10))}");
        }
    }

    // ------------------------------------------------------------------ E) files parse at all

    /// <summary>
    /// <c>EmbeddedTermVocabularySource.Translations</c> swallows <see cref="JsonException"/> so a
    /// malformed file cannot stop startup — which means a broken file ships as a silently EMPTY
    /// dictionary. This test reads the raw resources so the failure is loud here instead.
    /// </summary>
    [Fact]
    public void Every_embedded_term_file_is_a_flat_json_string_map()
    {
        var assembly = typeof(EmbeddedTermVocabularySource).Assembly;
        var names = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(TermResourcePrefix, StringComparison.Ordinal)
                        && n.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(names.Count >= Blocks.Count,
            $"Expected at least {Blocks.Count} embedded term files under '{TermResourcePrefix}', " +
            $"found {names.Count}: {string.Join(", ", names)}");

        foreach (var name in names)
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            var parsed = default(Dictionary<string, string>?);
            var ex = Record.Exception(() =>
            {
                parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
            });

            Assert.True(ex is null,
                $"Embedded resource '{name}' is not a valid flat JSON string map, and the loader " +
                $"swallows that error at runtime — it would ship as an empty dictionary: {ex?.Message}");
            Assert.True(parsed is { Count: > 0 }, $"Embedded resource '{name}' parsed to nothing.");
        }
    }

    // ------------------------------------------------------------------ F) the seed list itself

    /// <summary>
    /// Terms are stored and matched under <see cref="TermMatcher.NormaliseKey"/>, which trims,
    /// collapses internal whitespace and lowercases. Two seed entries that differ only in case or
    /// spacing collapse to one row, so the second is silently discarded by
    /// <c>TermSeedService.SeedTermsAsync</c> and can never receive its own translation.
    /// </summary>
    [Fact]
    public void The_panchangam_seed_has_no_terms_that_collide_once_normalised()
    {
        var all = PanchangamTermSeed.All();

        var collisions = all
            .GroupBy(TermMatcher.NormaliseKey, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => $"[{string.Join(" | ", g)}]")
            .ToList();

        Assert.True(collisions.Count == 0,
            "These seed terms collapse to the same normalised key, so only the first is ever stored " +
            $"and the rest can never be translated: {string.Join(", ", collisions)}");
    }

    /// <summary>
    /// <see cref="TermMatcher"/> drops any term shorter than two characters (it would match almost
    /// everywhere) or containing no letter (it would collide with the times, dates and separators the
    /// output has to preserve byte-for-byte). A seed entry that fails either rule is dead weight: it
    /// creates a dictionary row an admin must translate, which then never matches anything.
    /// </summary>
    [Fact]
    public void Every_panchangam_seed_term_is_usable_by_the_matcher()
    {
        var all = PanchangamTermSeed.All();
        Assert.NotEmpty(all);

        var unusable = all
            .Where(t => TermMatcher.NormaliseKey(t).Length < 2 || !t.Any(char.IsLetter))
            .ToList();

        Assert.True(unusable.Count == 0,
            "These seed terms are dropped at TermMatcher build time (under two characters, or no " +
            $"letter at all), so they can never match: {string.Join(", ", unusable.Select(t => $"\"{t}\""))}");
    }

    /// <summary>
    /// The seed is sourced from the Panchangam code tables so it cannot drift from the calculator.
    /// A refactor that broke that link would most likely show up as a suddenly tiny list, which every
    /// other test here would happily pass.
    /// </summary>
    [Fact]
    public void The_panchangam_seed_still_covers_the_whole_generated_vocabulary()
    {
        var all = PanchangamTermSeed.All();

        // 7 weekdays + 15 tithis + Amavasya + 27 nakshatras + 12 masams (+12 Adhika forms)
        // + 6 rutuvus + 60 samvatsaras + 8 literals and connectives, less overlaps.
        Assert.True(all.Count >= 140,
            $"The seed produced only {all.Count} terms; it is supposed to be ~150 drawn from the " +
            "Panchangam tables. Something has stopped reading those tables.");

        // One landmark from each table the seed is supposed to be reading, plus the inline literals
        // and the FormatSpans connectives, which live only in the calculator's format strings.
        string[] landmarks =
        [
            "Sunday", "Pournami", "Amavasya", "Uttarashada", "Adhika Jyeshtham",
            "Vasantha", "Akshaya", "Krishna (Bahula)", "Uttarayanam", "None",
            "upto", "from", "full day"
        ];

        var present = all.ToHashSet(StringComparer.Ordinal);
        var lost = landmarks.Where(l => !present.Contains(l)).ToList();

        Assert.True(lost.Count == 0,
            "The seed no longer produces terms the calculator can still emit, so those words will " +
            $"appear in English inside an otherwise translated line: {string.Join(", ", lost)}");
    }

    [Fact]
    public void The_panchangam_seed_is_stable_across_calls()
    {
        // The dictionary is imported repeatedly (startup, admin re-seed); an unstable order or set
        // would make SeedTermsAsync non-idempotent.
        var first = PanchangamTermSeed.All();
        var second = PanchangamTermSeed.All();

        Assert.True(first.SequenceEqual(second, StringComparer.Ordinal),
            "PanchangamTermSeed.All() returned a different sequence on a second call, so re-seeding " +
            "would add or reorder dictionary rows every time it runs.");
        Assert.Equal("panchangam", PanchangamTermSeed.Category);
    }

    /// <summary>
    /// The vocabulary source is what the seeder consumes; every term it offers must carry the
    /// panchangam category, or the category-scoped annotations on <c>PanchangamDto</c> will not find it.
    /// </summary>
    [Fact]
    public void The_embedded_vocabulary_offers_the_seed_under_the_panchangam_category()
    {
        var terms = new EmbeddedTermVocabularySource().Terms();

        Assert.Equal(PanchangamTermSeed.All().Count, terms.Count);
        Assert.All(terms, t => Assert.Equal(PanchangamTermSeed.Category, t.Category));
        Assert.Contains(terms, t => t.Source == "Amavasya");
    }
}
