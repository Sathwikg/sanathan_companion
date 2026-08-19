namespace App.Core.Services;

/// <summary>Whether the app is in front of the seeker right now.</summary>
/// <remarks>
/// Exists so shared components can stop doing periodic work the user cannot see. Pausing the
/// WebView is not enough on its own: a <see cref="System.Threading.Timer"/> lives on the .NET side,
/// which keeps running while the browser engine is suspended, so a component that ticks needs to be
/// told directly.
/// </remarks>
public interface IAppLifecycle
{
    /// <summary>False while the app is backgrounded or the screen is off.</summary>
    bool IsActive { get; }

    /// <summary>Raised when <see cref="IsActive"/> changes.</summary>
    event Action? Changed;
}

/// <summary>
/// What the web host gets. A browser tab that is hidden already has its timers throttled hard by
/// the browser itself, so there is nothing useful to add and no lifecycle to hook.
/// </summary>
public sealed class AlwaysActiveLifecycle : IAppLifecycle
{
    public bool IsActive => true;

    public event Action? Changed
    {
        add { }      // never fires; the accessors exist only to satisfy the interface
        remove { }
    }
}
