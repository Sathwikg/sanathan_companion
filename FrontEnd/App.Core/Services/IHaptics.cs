namespace App.Core.Services;

/// <summary>Vibration for the japa screen. One implementation per host.</summary>
/// <remarks>
/// Same split as <see cref="IImageShare"/>: in a browser the vibration is started from JavaScript
/// inside the tap handler itself (wwwroot/japa.js), where it is immediate and always counts as a
/// user gesture, so the web default does nothing here. A WebView's <c>navigator.vibrate</c> is not
/// wired to the motor on either phone platform, so the MAUI host implements this natively.
/// </remarks>
public interface IHaptics
{
    /// <summary>True when <see cref="Tick"/> and <see cref="Celebrate"/> drive the device motor.</summary>
    bool IsNative { get; }

    /// <summary>A short, crisp tick for one counted repetition.</summary>
    void Tick();

    /// <summary>A long vibration for a completed mala, unmistakable even mid-chant.</summary>
    void Celebrate();
}

/// <summary>The web default: the browser vibrates from japa.js, so there is nothing to do here.</summary>
public sealed class BrowserHaptics : IHaptics
{
    public bool IsNative => false;
    public void Tick() { }
    public void Celebrate() { }
}
