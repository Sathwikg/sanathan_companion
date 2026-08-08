using System.Text.RegularExpressions;
using Sanathana.Companion.Infrastructure.Localization;

namespace Sanathana.Companion.Tests;

/// <summary>
/// Cross-checks the Blazor markup against the shipped English catalogue. A key referenced in a
/// razor file but absent from the resource files silently renders its inline fallback forever and
/// can never be translated — this test makes that a build failure instead of a mystery.
/// </summary>
public class LocalizationKeyUsageTests
{
    /// <summary>Matches Loc["key"…], Loc["key", "fallback"] and Loc.TF("key", …).</summary>
    private static readonly Regex LocKey =
        new(@"Loc(?:\s*\[\s*|\s*\.\s*TF\s*\(\s*)""(?<key>[A-Za-z][A-Za-z0-9_]*(?:\.[A-Za-z0-9_]+)+)""",
            RegexOptions.Compiled);

    /// <summary>Walks up from the test binaries to the repo root, then into the shared UI project.</summary>
    private static DirectoryInfo? FindSharedUi()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "FrontEnd", "App.UI.Shared");
            if (Directory.Exists(candidate)) return new DirectoryInfo(candidate);
            dir = dir.Parent;
        }
        return null;
    }

    [Fact]
    public void Every_localization_key_used_in_markup_exists_in_the_english_catalogue()
    {
        var sharedUi = FindSharedUi();
        // The frontend is not part of a backend-only checkout; nothing to verify then.
        if (sharedUi is null) return;

        var english = LocalizationSeedFiles.Load()["en"];
        var razorFiles = sharedUi.GetFiles("*.razor", SearchOption.AllDirectories);
        Assert.NotEmpty(razorFiles);

        var unknown = new SortedSet<string>(StringComparer.Ordinal);
        var referenced = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var file in razorFiles)
        {
            foreach (Match match in LocKey.Matches(File.ReadAllText(file.FullName)))
            {
                var key = match.Groups["key"].Value;
                referenced.Add(key);
                if (!english.ContainsKey(key))
                    unknown.Add($"{key}  ({file.Name})");
            }
        }

        Assert.True(referenced.Count > 0, "No Loc[...] usages were found — the regex or the sweep is wrong.");
        Assert.True(unknown.Count == 0,
            $"{unknown.Count} localization key(s) are used in markup but missing from the English resource files:{Environment.NewLine}" +
            string.Join(Environment.NewLine, unknown.Take(25)));
    }

    [Fact]
    public void Route_maps_to_the_namespace_that_owns_a_form()
    {
        // This mapping is what lets the editor be scoped form by form.
        Assert.Equal("deities", Application.Services.LocalizationService.NamespaceForRoute("/deities"));
        Assert.Equal("chantsConfig", Application.Services.LocalizationService.NamespaceForRoute("/chants-config"));
        Assert.Equal("myNotifications", Application.Services.LocalizationService.NamespaceForRoute("/my-notifications"));
        Assert.Equal("accessRights", Application.Services.LocalizationService.NamespaceForRoute("access-rights"));

        // Sub-routes still belong to the same form.
        Assert.Equal("chantsConfig", Application.Services.LocalizationService.NamespaceForRoute("/chants-config/{id}/edit"));

        Assert.Equal(string.Empty, Application.Services.LocalizationService.NamespaceForRoute("/"));
        Assert.Equal(string.Empty, Application.Services.LocalizationService.NamespaceForRoute(""));
    }
}
