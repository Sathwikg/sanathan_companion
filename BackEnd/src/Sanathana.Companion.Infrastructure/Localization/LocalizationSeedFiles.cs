using System.Reflection;
using System.Text.Json;

namespace Sanathana.Companion.Infrastructure.Localization;

/// <summary>
/// Reads the translation seed files that are embedded in this assembly.
/// Resource names look like
/// <c>Sanathana.Companion.Infrastructure.Localization.Resources.common.common.te.json</c>
/// — i.e. <c>…Resources.{namespace}.{namespace}.{languageCode}.json</c>.
/// </summary>
public static class LocalizationSeedFiles
{
    private const string Prefix = "Sanathana.Companion.Infrastructure.Localization.Resources.";

    /// <summary>All seed entries grouped by language code, e.g. "te" -> { "common.save": "…" }.</summary>
    public static IReadOnlyDictionary<string, Dictionary<string, string>> Load()
    {
        var byLanguage = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var asm = typeof(LocalizationSeedFiles).Assembly;

        foreach (var resourceName in asm.GetManifestResourceNames())
        {
            if (!resourceName.StartsWith(Prefix, StringComparison.Ordinal) ||
                !resourceName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                continue;

            var code = ExtractLanguageCode(resourceName);
            if (code is null) continue;

            var entries = ReadEntries(asm, resourceName);
            if (entries.Count == 0) continue;

            if (!byLanguage.TryGetValue(code, out var bucket))
                byLanguage[code] = bucket = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var (key, value) in entries)
                bucket[key] = value;
        }

        return byLanguage;
    }

    /// <summary>"…Resources.common.common.te.json" -> "te".</summary>
    private static string? ExtractLanguageCode(string resourceName)
    {
        // Strip the ".json" suffix, then take the segment before it.
        var withoutExtension = resourceName[..^".json".Length];
        var lastDot = withoutExtension.LastIndexOf('.');
        if (lastDot < 0) return null;

        var code = withoutExtension[(lastDot + 1)..];
        // Language codes are 2–5 chars (en, te, pt-BR); anything else is a malformed file name.
        return code.Length is >= 2 and <= 5 ? code.ToLowerInvariant() : null;
    }

    private static Dictionary<string, string> ReadEntries(Assembly asm, string resourceName)
    {
        using var stream = asm.GetManifestResourceStream(resourceName);
        if (stream is null) return new Dictionary<string, string>();

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
            return parsed ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            // A malformed seed file must not stop the API from starting; it is surfaced by the
            // localization tests instead, which assert every embedded file parses.
            return new Dictionary<string, string>();
        }
    }

    /// <summary>The part of a key before the first dot ("common.save" -> "common").</summary>
    public static string NamespaceOf(string key)
    {
        var dot = key.IndexOf('.');
        return dot <= 0 ? "general" : key[..dot];
    }
}
