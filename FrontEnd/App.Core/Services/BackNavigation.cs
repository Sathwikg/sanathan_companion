using Microsoft.AspNetCore.Components;

namespace App.Core.Services;

/// <summary>
/// The overlays open right now (sheets, dialogs, the notification panel, a picture preview), most
/// recent last, so Back closes the one on top before it ever leaves the screen. Every phone app
/// works this way: Back on an open sheet closes the sheet.
/// </summary>
/// <remarks>
/// Scoped: one per app session, shared by every overlay component and the back navigator.
/// </remarks>
public sealed class OverlayStack
{
    private readonly List<Entry> _open = new();

    private sealed class Entry : IDisposable
    {
        private readonly OverlayStack _owner;
        public Func<Task> Close { get; }

        public Entry(OverlayStack owner, Func<Task> close)
        {
            _owner = owner;
            Close = close;
        }

        public void Dispose()
        {
            lock (_owner._open) _owner._open.Remove(this);
        }
    }

    public int Count
    {
        get { lock (_open) return _open.Count; }
    }

    /// <summary>Records an overlay as open. Dispose the result when it closes.</summary>
    public IDisposable Push(Func<Task> close)
    {
        var entry = new Entry(this, close);
        lock (_open) _open.Add(entry);
        return entry;
    }

    /// <summary>Closes the overlay on top. False when none is open.</summary>
    public async Task<bool> TryCloseTopAsync()
    {
        Entry? top;
        lock (_open) top = _open.Count == 0 ? null : _open[^1];
        if (top is null) return false;

        // Removed first: the close callback re-renders, and a component that is slow to report
        // itself closed must not be asked to close twice by a second press.
        top.Dispose();
        await top.Close();
        return true;
    }
}

/// <summary>
/// Keeps one overlay component's entry in <see cref="OverlayStack"/> in step with whether it is
/// showing. Call <see cref="Sync"/> after every render; dispose with the component.
/// </summary>
public sealed class OverlayRegistration : IDisposable
{
    private readonly OverlayStack _stack;
    private IDisposable? _entry;

    public OverlayRegistration(OverlayStack stack) => _stack = stack;

    public void Sync(bool open, Func<Task> close)
    {
        if (open && _entry is null) _entry = _stack.Push(close);
        else if (!open && _entry is not null) Dispose();
    }

    public void Dispose()
    {
        _entry?.Dispose();
        _entry = null;
    }
}

/// <summary>
/// The WebView's own history, which only the native host can read. The web host has none to offer.
/// </summary>
public interface INativeNavigation
{
    bool IsNative { get; }

    /// <summary>True when the WebView has an earlier page in its history.</summary>
    bool CanGoBack();

    /// <summary>The path of that earlier page ("sadhana", "login"), or null when there is none.</summary>
    string? PreviousPath();

    void GoBack();

    /// <summary>
    /// Puts the app in the background, the way Back on a home screen does in every Android app: it
    /// stays in Recents and opens where it was. Nothing on iOS, where apps never leave themselves.
    /// </summary>
    void LeaveApp();
}

/// <summary>The web host: the browser's own Back button and history already do all of this.</summary>
public sealed class BrowserNavigation : INativeNavigation
{
    public bool IsNative => false;
    public bool CanGoBack() => false;
    public string? PreviousPath() => null;
    public void GoBack() { }
    public void LeaveApp() { }
}

/// <summary>
/// Carries Android's Back button from the native page, which hears it, to the Blazor layout, which
/// knows what is open and where "back" is. A singleton for the same reason as <see cref="DeepLinks"/>.
/// </summary>
public sealed class HardwareBackButton
{
    /// <summary>Set by the layout while it is on screen. Returns true when it handled the press.</summary>
    public Func<Task<bool>>? Handler { get; set; }

    public Task<bool> PressAsync() => Handler?.Invoke() ?? Task.FromResult(false);
}

/// <summary>
/// One answer to "Back", shared by Android's Back button, the iPhone's edge swipe and the back arrow
/// in the top bar, so all three agree.
/// </summary>
/// <remarks>
/// In order: close the overlay on top; on the home screen, report "not handled" (Android then puts
/// the app in the background, as every app does from its home); otherwise go to the previous page,
/// unless that page is the sign-in screen (Back after signing in must not ask for the password
/// again); and when there is no previous page at all (the app was opened from a notification
/// straight onto an inner page), go home rather than leave.
/// </remarks>
public sealed class AppBackNavigator
{
    /// <summary>Pages Back must never return to once past them.</summary>
    private static readonly string[] EntryPages = { "login", "register", "features", "download", "access-denied" };

    private readonly NavigationManager _nav;
    private readonly OverlayStack _overlays;
    private readonly INativeNavigation _native;

    public AppBackNavigator(NavigationManager nav, OverlayStack overlays, INativeNavigation native)
    {
        _nav = nav;
        _overlays = overlays;
        _native = native;
    }

    /// <summary>The phone's home: the app root and the mobile dashboard it opens on.</summary>
    public static bool IsHomePath(string? path)
    {
        var p = Clean(path);
        return p.Length == 0 || p.Equals("mobile-dashboard", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsEntryPath(string? path)
    {
        var first = Clean(path).Split('/')[0];
        return EntryPages.Any(e => e.Equals(first, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>"/sadhana/chant/x?y#z" → "sadhana/chant/x".</summary>
    public static string Clean(string? path)
    {
        var value = path ?? string.Empty;
        var cut = value.IndexOfAny(new[] { '?', '#' });
        if (cut >= 0) value = value[..cut];
        return value.Trim().Trim('/');
    }

    public string CurrentPath => Clean(_nav.ToBaseRelativePath(_nav.Uri));

    /// <returns>False only when Back should leave the app (the home screen with nothing open).</returns>
    public async Task<bool> BackAsync()
    {
        if (await _overlays.TryCloseTopAsync()) return true;

        var current = CurrentPath;
        if (IsHomePath(current)) return false;

        // The sign-in pages themselves: Back there is the system's to decide.
        if (IsEntryPath(current)) return false;

        if (_native.CanGoBack() && !IsEntryPath(_native.PreviousPath()))
        {
            _native.GoBack();
            return true;
        }

        _nav.NavigateTo(string.Empty, replace: true);
        return true;
    }
}
