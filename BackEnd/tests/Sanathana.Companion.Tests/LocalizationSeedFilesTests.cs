using System.Text.Json;
using System.Text.RegularExpressions;
using Sanathana.Companion.Infrastructure.Localization;

namespace Sanathana.Companion.Tests;

/// <summary>
/// Guards the shipped translation files themselves: every language must cover every English key,
/// keep its placeholders, and be written in its own script. These catch a bad translation before
/// it ever reaches the database.
/// </summary>
public class LocalizationSeedFilesTests
{
    private const string BaseCode = "en";

    /// <summary>Unicode block each language must be written in, and must NOT stray outside.</summary>
    private static readonly Dictionary<string, (int Lo, int Hi, string Script)> Blocks = new()
    {
        ["te"] = (0x0C00, 0x0C7F, "Telugu"),
        ["hi"] = (0x0900, 0x097F, "Devanagari"),
        ["ta"] = (0x0B80, 0x0BFF, "Tamil"),
        ["kn"] = (0x0C80, 0x0CFF, "Kannada"),
    };

    private static readonly Regex Placeholder = new(@"\{(\d+)\}", RegexOptions.Compiled);

    private static IReadOnlyDictionary<string, Dictionary<string, string>> Load() => LocalizationSeedFiles.Load();

    [Fact]
    public void Embedded_seed_files_are_present_and_parse()
    {
        var all = Load();

        Assert.True(all.ContainsKey(BaseCode), "The English base bundle is missing from the embedded resources.");
        Assert.NotEmpty(all[BaseCode]);

        foreach (var code in Blocks.Keys)
            Assert.True(all.ContainsKey(code), $"No embedded seed file was found for '{code}'.");
    }

    [Fact]
    public void Every_language_covers_every_english_key()
    {
        var all = Load();
        var english = all[BaseCode];

        foreach (var code in Blocks.Keys)
        {
            var missing = english.Keys.Where(k => !all[code].ContainsKey(k)).OrderBy(k => k).ToList();
            Assert.True(missing.Count == 0,
                $"'{code}' is missing {missing.Count} key(s): {string.Join(", ", missing.Take(10))}");
        }
    }

    [Fact]
    public void No_language_defines_a_key_english_does_not_have()
    {
        var all = Load();
        var english = all[BaseCode];

        foreach (var code in Blocks.Keys)
        {
            var extra = all[code].Keys.Where(k => !english.ContainsKey(k)).OrderBy(k => k).ToList();
            Assert.True(extra.Count == 0,
                $"'{code}' defines {extra.Count} key(s) absent from English: {string.Join(", ", extra.Take(10))}");
        }
    }

    [Fact]
    public void No_translation_is_blank()
    {
        var all = Load();

        foreach (var (code, entries) in all)
        {
            var blank = entries.Where(e => string.IsNullOrWhiteSpace(e.Value)).Select(e => e.Key).ToList();
            Assert.True(blank.Count == 0, $"'{code}' has blank value(s): {string.Join(", ", blank.Take(10))}");
        }
    }

    [Fact]
    public void Placeholders_survive_translation()
    {
        var all = Load();
        var english = all[BaseCode];

        foreach (var code in Blocks.Keys)
        {
            foreach (var (key, englishValue) in english)
            {
                if (!all[code].TryGetValue(key, out var translated)) continue;

                var expected = Placeholder.Matches(englishValue).Select(m => m.Value).OrderBy(v => v).ToList();
                var actual = Placeholder.Matches(translated).Select(m => m.Value).OrderBy(v => v).ToList();

                Assert.True(expected.SequenceEqual(actual),
                    $"'{code}' key '{key}' changed placeholders: expected [{string.Join(",", expected)}] " +
                    $"but found [{string.Join(",", actual)}] in \"{translated}\".");
            }
        }
    }

    [Fact]
    public void Each_language_is_written_in_its_own_script()
    {
        var all = Load();
        var english = all[BaseCode];

        foreach (var (code, (lo, hi, script)) in Blocks)
        {
            foreach (var (key, value) in all[code])
            {
                // Only assert on entries whose English source is real prose; a value that is
                // purely a placeholder or symbol has nothing to translate.
                if (!english.TryGetValue(key, out var source) || !Regex.IsMatch(source, "[A-Za-z]{3}")) continue;

                Assert.True(value.Any(ch => ch >= lo && ch <= hi),
                    $"'{code}' key '{key}' contains no {script} characters: \"{value}\".");
            }
        }
    }

    /// <summary>
    /// Telugu (U+0C00–U+0C7F) and Kannada (U+0C80–U+0CFF) are adjacent blocks that look alike,
    /// and Tamil is easy to confuse with both — so cross-contamination is the likeliest defect.
    /// </summary>
    [Fact]
    public void No_language_leaks_characters_from_a_sibling_script()
    {
        var all = Load();

        foreach (var (code, _) in Blocks)
        {
            foreach (var (otherCode, (lo, hi, script)) in Blocks.Where(b => b.Key != code))
            {
                foreach (var (key, value) in all[code])
                {
                    var offending = value.FirstOrDefault(ch => ch >= lo && ch <= hi);
                    Assert.True(offending == '\0',
                        $"'{code}' key '{key}' contains {script} character U+{(int)offending:X4} in \"{value}\".");
                }
            }
        }
    }

    [Fact]
    public void Translations_are_not_just_the_english_text_copied()
    {
        var all = Load();
        var english = all[BaseCode];

        foreach (var code in Blocks.Keys)
        {
            var copied = english
                .Where(e => Regex.IsMatch(e.Value, "[A-Za-z]{3}")
                            && all[code].TryGetValue(e.Key, out var v)
                            && string.Equals(v.Trim(), e.Value.Trim(), StringComparison.Ordinal))
                .Select(e => e.Key)
                .ToList();

            Assert.True(copied.Count == 0,
                $"'{code}' left {copied.Count} value(s) as untranslated English: {string.Join(", ", copied.Take(10))}");
        }
    }

    [Fact]
    public void Namespace_is_derived_from_the_key_prefix()
    {
        Assert.Equal("common", LocalizationSeedFiles.NamespaceOf("common.save"));
        Assert.Equal("pages", LocalizationSeedFiles.NamespaceOf("pages.deities.title"));
        Assert.Equal("general", LocalizationSeedFiles.NamespaceOf("noprefix"));
    }

    [Fact]
    public void Every_embedded_file_is_valid_json()
    {
        var asm = typeof(LocalizationSeedFiles).Assembly;
        var names = asm.GetManifestResourceNames()
            .Where(n => n.Contains(".Localization.Resources.") && n.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(names);

        foreach (var name in names)
        {
            using var stream = asm.GetManifestResourceStream(name)!;
            var ex = Record.Exception(() => JsonSerializer.Deserialize<Dictionary<string, string>>(stream));
            Assert.True(ex is null, $"Embedded resource '{name}' is not a valid flat JSON string map: {ex?.Message}");
        }
    }
}
