using App.Core.Services;

namespace App.Mobile.Services;

/// <summary>
/// The japa screen's vibration through the device motor, replacing the browser path the web host
/// uses — a WebView's <c>navigator.vibrate</c> never reaches the motor on either platform.
/// </summary>
/// <remarks>
/// The tick differs by platform on purpose. On Android a 14 ms one-shot is a crisp tap on every
/// motor and does not depend on the system "touch feedback" switch that HapticFeedback obeys. On
/// iOS, Vibration ignores the duration and always buzzes for about 400 ms, which would be a full
/// buzz per chant, so the tick uses the Taptic Engine's click instead. The completed mala is the
/// long one everywhere. Every call is guarded: a phone without a motor must still count.
/// </remarks>
public sealed class MauiHaptics : IHaptics
{
    private static readonly TimeSpan TickLength = TimeSpan.FromMilliseconds(14);
    private static readonly TimeSpan CelebrateLength = TimeSpan.FromMilliseconds(650);

    public bool IsNative => true;

    public void Tick() => Run(() =>
    {
        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            if (Vibration.Default.IsSupported) Vibration.Default.Vibrate(TickLength);
        }
        else if (HapticFeedback.Default.IsSupported)
        {
            HapticFeedback.Default.Perform(HapticFeedbackType.Click);
        }
    });

    public void Celebrate() => Run(() =>
    {
        if (Vibration.Default.IsSupported) Vibration.Default.Vibrate(CelebrateLength);
        else if (HapticFeedback.Default.IsSupported) HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
    });

    private static void Run(Action vibrate)
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try { vibrate(); }
                catch { /* no motor, or the permission was stripped from the manifest */ }
            });
        }
        catch { /* the app is shutting down */ }
    }
}
