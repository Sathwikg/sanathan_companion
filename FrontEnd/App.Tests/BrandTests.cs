using App.Core.Config;

namespace App.Tests;

/// <summary>
/// The app's name and logo are defined once, in FrontEnd/Branding/Branding.props. These checks
/// keep it that way: the values must reach the code, and nothing may spell the name out again.
/// </summary>
public class BrandTests
{
    [Fact]
    public void Branding_props_reaches_the_brand_class()
    {
        Assert.False(string.IsNullOrWhiteSpace(Brand.AppName), "Brand.AppName is empty: Branding.props was not baked into App.Core.");
        Assert.True(Brand.LogoImageUrl is not null || !string.IsNullOrWhiteSpace(Brand.LogoGlyph),
            "The logo has neither an image nor a glyph.");
    }

    [Fact]
    public void Configured_logo_image_exists()
    {
        if (Brand.LogoImageUrl is not { } url) return;

        var file = Path.Combine(FindFrontEnd().FullName, "Branding", Path.GetFileName(url));
        Assert.True(File.Exists(file), $"Branding.props names a logo image that is not in FrontEnd/Branding: {file}");
    }

    /// <summary>
    /// A hardcoded copy of the name is exactly what a rebrand misses. Every surface has a way to
    /// get it from Branding.props: Brand.AppName in code, {{Brand:AppName}} in an HTML template,
    /// $(BrandAppName) in a project file, "this app" in a permission string.
    /// </summary>
    [Fact]
    public void Nothing_outside_branding_spells_out_the_app_name()
    {
        var root = FindFrontEnd();
        string[] extensions = [".razor", ".cs", ".html", ".js", ".css", ".json", ".xml", ".plist", ".xaml", ".csproj", ".appxmanifest"];
        string[] skipped = ["bin", "obj", "Branding", "App.Tests", "lib"];

        var offenders = root.EnumerateFiles("*", SearchOption.AllDirectories)
            .Where(f => extensions.Contains(f.Extension, StringComparer.OrdinalIgnoreCase))
            .Where(f => !Path.GetRelativePath(root.FullName, f.FullName)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => skipped.Contains(segment, StringComparer.OrdinalIgnoreCase)))
            .Where(f => File.ReadAllText(f.FullName).Contains(Brand.AppName, StringComparison.OrdinalIgnoreCase))
            .Select(f => Path.GetRelativePath(root.FullName, f.FullName))
            .ToList();

        Assert.True(offenders.Count == 0,
            $"These files hardcode \"{Brand.AppName}\"; read it from FrontEnd/Branding instead:\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>Walks up from the test binaries to the FrontEnd folder.</summary>
    private static DirectoryInfo FindFrontEnd()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Branding", "Branding.props")))
                return dir;
        }

        throw new DirectoryNotFoundException("FrontEnd/Branding/Branding.props was not found above the test binaries.");
    }
}
