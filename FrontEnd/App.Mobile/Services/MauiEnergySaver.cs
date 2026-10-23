using App.Core.Services;

namespace App.Mobile.Services;

/// <summary>
/// The phone's own battery saver (Android Battery Saver, iOS Low Power Mode), so the japa screen can
/// lighten itself while it is on. Reading it needs no permission on either platform.
/// </summary>
public sealed class MauiEnergySaver : IEnergySaver
{
    public MauiEnergySaver()
    {
        try { Battery.Default.EnergySaverStatusChanged += (_, _) => Changed?.Invoke(); }
        catch { /* a head without the API simply never reports a change */ }
    }

    public bool IsOn
    {
        get
        {
            try { return Battery.Default.EnergySaverStatus == EnergySaverStatus.On; }
            catch { return false; }
        }
    }

    public event Action? Changed;
}
