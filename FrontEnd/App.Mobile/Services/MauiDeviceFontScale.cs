using App.Core.Services;

namespace App.Mobile.Services;

/// <summary>Reads the phone's own text-size setting. See <see cref="IDeviceFontScale"/>.</summary>
/// <remarks>
/// Read fresh on every call rather than cached: the seeker can change the setting while the app is
/// in the background, and the app re-applies it each time it comes back to the front.
/// </remarks>
public sealed class MauiDeviceFontScale : IDeviceFontScale
{
    public bool IsNative => true;

    public double Read()
    {
        try
        {
#if ANDROID
            // The activity's configuration first: MainActivity handles font-size changes itself (so
            // the app is not restarted), and its resources are the ones Android updates in place.
            var configuration = Platform.CurrentActivity?.Resources?.Configuration
                                ?? global::Android.App.Application.Context.Resources?.Configuration;
            return FontScaleLimits.Clamp(configuration?.FontScale ?? 1f);
#elif IOS || MACCATALYST
            // Dynamic Type has no multiplier of its own; the body text style's size against its
            // default of 17 points is the one every iOS app's text grows by.
            var body = UIKit.UIFont.GetPreferredFontForTextStyle(UIKit.UIFontTextStyle.Body);
            return body is null ? 1 : FontScaleLimits.Clamp(body.PointSize / 17.0);
#else
            return 1;
#endif
        }
        catch (Exception)
        {
            // Never let reading a preference cost the seeker the app.
            return 1;
        }
    }
}
