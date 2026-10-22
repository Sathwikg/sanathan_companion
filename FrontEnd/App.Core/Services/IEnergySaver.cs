namespace App.Core.Services;

/// <summary>Whether the device itself is saving battery, so the app can ask less of it too.</summary>
/// <remarks>
/// The japa screen follows it: while the phone's battery saver is on, the screen drops its tap
/// animations, saves less often and vibrates only when a mala completes, exactly as when the seeker
/// switches on the screen's own Battery Saver.
/// </remarks>
public interface IEnergySaver
{
    bool IsOn { get; }

    /// <summary>Raised when <see cref="IsOn"/> changes.</summary>
    event Action? Changed;
}

/// <summary>
/// The web default. A browser exposes no battery-saver signal; the page's own toggle and the
/// system's reduced-motion setting (honoured in CSS) are what it has.
/// </summary>
public sealed class NoEnergySaver : IEnergySaver
{
    public bool IsOn => false;

    public event Action? Changed
    {
        add { }      // never fires; the accessors exist only to satisfy the interface
        remove { }
    }
}
