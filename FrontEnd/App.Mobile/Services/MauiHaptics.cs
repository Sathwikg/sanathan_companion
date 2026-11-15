using App.Core.Services;

namespace App.Mobile.Services;

/// <summary>
/// The japa screen's vibration through the device motor, replacing the browser path the web host
/// uses — a WebView's <c>navigator.vibrate</c> never reaches the motor on either platform.
/// </summary>
/// <remarks>
/// <b>Android</b> drives the vibrator directly rather than through MAUI's Vibration, for two reasons
/// that both made the tap tick impossible to feel:
/// <list type="bullet">
/// <item>The tick used to be 14 ms. The coin-type (ERM) motors in most budget and mid-range Android
/// phones need roughly 25 ms just to spin up, so 14 ms produced no perceptible buzz at all. It is now
/// <see cref="TickLength"/>, which reads as a firm tap on those motors and a crisp click on the
/// linear (LRA) motors in flagships.</item>
/// <item>MAUI asks for the device's DEFAULT amplitude, which several manufacturers set low. On a phone
/// with amplitude control the tick now asks for full strength; the duration keeps it short.</item>
/// </list>
/// The call is made on the calling thread: the vibrator service is thread-safe, and the hop onto
/// the main thread added a frame of delay between the finger and the buzz.
/// <para>
/// <b>iOS</b>: Vibration ignores the duration and always buzzes for about 400 ms, which would be a
/// full buzz per chant, so the tick uses the Taptic Engine's click instead. The completed mala is
/// the long one everywhere. Every call is guarded: a phone without a motor must still count.
/// </para>
/// </remarks>
public sealed class MauiHaptics : IHaptics
{
    internal static readonly TimeSpan TickLength = TimeSpan.FromMilliseconds(30);
    private static readonly TimeSpan CelebrateLength = TimeSpan.FromMilliseconds(650);

    public bool IsNative => true;

    public void Tick()
    {
#if ANDROID
        if (AndroidVibrate((long)TickLength.TotalMilliseconds, fullStrength: true)) return;
#endif
        Run(() =>
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
    }

    public void Celebrate()
    {
#if ANDROID
        if (AndroidVibrate((long)CelebrateLength.TotalMilliseconds, fullStrength: true)) return;
#endif
        Run(() =>
        {
            if (Vibration.Default.IsSupported) Vibration.Default.Vibrate(CelebrateLength);
            else if (HapticFeedback.Default.IsSupported) HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
        });
    }

#if ANDROID
    private static global::Android.OS.Vibrator? _vibrator;
    private static bool _resolved;

    /// <summary>The phone's vibrator, looked up once. Null when it has none.</summary>
    private static global::Android.OS.Vibrator? Vibrator
    {
        get
        {
            if (_resolved) return _vibrator;

            var context = global::Android.App.Application.Context;
            global::Android.OS.Vibrator? vibrator = null;

            if (OperatingSystem.IsAndroidVersionAtLeast(31))
            {
                var manager = context.GetSystemService(global::Android.Content.Context.VibratorManagerService) as global::Android.OS.VibratorManager;
                vibrator = manager?.DefaultVibrator;
            }
            else
            {
#pragma warning disable CA1422 // VibratorService is the only way below Android 12
                vibrator = context.GetSystemService(global::Android.Content.Context.VibratorService) as global::Android.OS.Vibrator;
#pragma warning restore CA1422
            }

            _vibrator = vibrator is not null && vibrator.HasVibrator ? vibrator : null;
            _resolved = true;
            return _vibrator;
        }
    }

    /// <summary>Vibrates once. False when this phone cannot, so the caller can fall back.</summary>
    private static bool AndroidVibrate(long milliseconds, bool fullStrength)
    {
        try
        {
            var vibrator = Vibrator;
            if (vibrator is null) return false;

            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                var amplitude = fullStrength && vibrator.HasAmplitudeControl
                    ? 255
                    : global::Android.OS.VibrationEffect.DefaultAmplitude;

                var effect = global::Android.OS.VibrationEffect.CreateOneShot(milliseconds, amplitude);
                if (effect is null) return false;
                vibrator.Vibrate(effect);
            }
            else
            {
#pragma warning disable CA1422 // the only call that exists below Android 8
                vibrator.Vibrate(milliseconds);
#pragma warning restore CA1422
            }

            return true;
        }
        catch (Exception)
        {
            // The permission stripped from a modified build, or a vendor vibrator service that
            // throws. Let the MAUI path try; it is guarded too.
            return false;
        }
    }
#endif

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
