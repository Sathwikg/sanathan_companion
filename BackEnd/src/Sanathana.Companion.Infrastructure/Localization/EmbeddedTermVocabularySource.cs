using System.Reflection;
using System.Text.Json;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Infrastructure.Localization;

/// <summary>
/// The shipped vocabulary: source terms come from the Panchangam code tables, translations from
/// embedded JSON under <c>Localization/Terms/terms.{code}.json</c>.
/// </summary>
public class EmbeddedTermVocabularySource : ITermVocabularySource
{
    private const string Prefix = "Sanathana.Companion.Infrastructure.Localization.Terms.";

    public IReadOnlyList<(string Source, string Category)> Terms()
        => PanchangamTermSeed.All()
            .Select(t => (Source: t, Category: PanchangamTermSeed.Category))
            .ToList();

    public IReadOnlyDictionary<string, Dictionary<string, string>> Translations()
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var asm = typeof(EmbeddedTermVocabularySource).Assembly;

        foreach (var name in asm.GetManifestResourceNames())
        {
            if (!name.StartsWith(Prefix, StringComparison.Ordinal) ||
                !name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;

            var code = ExtractCode(name);
            if (code is null) continue;

            using var stream = asm.GetManifestResourceStream(name);
            if (stream is null) continue;

            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
                if (parsed is { Count: > 0 }) result[code] = parsed;
            }
            catch (JsonException)
            {
                // A malformed file must not stop startup; the localization tests assert validity.
            }
        }

        return result;
    }

    /// <summary>"…Terms.terms.te.json" -&gt; "te".</summary>
    private static string? ExtractCode(string resourceName)
    {
        var withoutExtension = resourceName[..^".json".Length];
        var lastDot = withoutExtension.LastIndexOf('.');
        if (lastDot < 0) return null;
        var code = withoutExtension[(lastDot + 1)..];
        return code.Length is >= 2 and <= 5 ? code.ToLowerInvariant() : null;
    }
}
