namespace App.Core.Services;

/// <summary>
/// The text size the seeker chose in their phone's own settings: Android's Font size, iOS's
/// Dynamic Type (Settings, Display &amp; Brightness, Text Size, and the larger Accessibility sizes).
/// </summary>
/// <remarks>
/// A web view does not follow that setting on its own in any useful way. Android's applies it as a
/// text-only zoom that ignores the layout around the text, so labels outgrow their bars; iOS's
/// ignores it entirely. So the host reads the setting natively and the app applies it as the root
/// font size, which every rem in the stylesheets is measured from: text, spacing and the boxes
/// around them grow together, the way a native screen does.
/// </remarks>
public interface IDeviceFontScale
{
    /// <summary>True when <see cref="Read"/> reports the device's own setting.</summary>
    bool IsNative { get; }

    /// <summary>The current setting as a multiplier: 1 is the default size, 1.3 is "large".</summary>
    double Read();
}

/// <summary>What the web host gets: a browser applies its own text size to rem, so there is nothing to add.</summary>
public sealed class BrowserFontScale : IDeviceFontScale
{
    public bool IsNative => false;
    public double Read() => 1;
}

/// <summary>The limits the app applies a device's text size within.</summary>
public static class FontScaleLimits
{
    /// <summary>The smallest scale followed; below it Indic conjuncts stop being legible.</summary>
    public const double Min = 0.85;

    /// <summary>
    /// The largest scale followed for content. Twice the default covers Android's largest size and
    /// iOS's first Accessibility sizes; past it a single word fills a line on a phone.
    /// </summary>
    public const double Max = 2.0;

    /// <summary>A reading the platform could not give (zero, NaN) is the default size.</summary>
    public static double Clamp(double scale)
        => double.IsFinite(scale) && scale > 0 ? Math.Clamp(scale, Min, Max) : 1;
}
