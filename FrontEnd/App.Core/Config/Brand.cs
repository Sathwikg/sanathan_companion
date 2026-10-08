using System.Reflection;

namespace App.Core.Config;

/// <summary>
/// The app's name and logo, as defined in <c>FrontEnd/Branding/Branding.props</c>.
/// </summary>
/// <remarks>
/// That file is the only place either is spelled out. The build bakes its values into this
/// assembly (see App.Core.csproj), so every host and every screen reads the same answer and a
/// rebrand is an edit there plus a rebuild. Nothing in the UI should hardcode the name or the
/// glyph; render <c>&lt;AppLogo /&gt;</c> for the logo and <see cref="AppName"/> for the name.
/// <para>
/// Not translated on purpose: a product name is the same word in every language, and a
/// per-language copy in the dictionary would be a second place to change it.
/// </para>
/// </remarks>
public static class Brand
{
    /// <summary>The product name: launcher, store listing, top bar, login, About.</summary>
    public static string AppName { get; } = Read(nameof(AppName));

    /// <summary>The mark drawn in the logo tile when there is no <see cref="LogoImageUrl"/>.</summary>
    public static string LogoGlyph { get; } = Read(nameof(LogoGlyph));

    /// <summary>
    /// Path of the logo image relative to the app's base href, or null to draw
    /// <see cref="LogoGlyph"/> in the tile instead.
    /// </summary>
    public static string? LogoImageUrl { get; } = NullIfEmpty(Read(nameof(LogoImageUrl)));

    private static string Read(string key) =>
        typeof(Brand).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "Brand." + key)?.Value ?? string.Empty;

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
