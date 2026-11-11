namespace App.Core.Services;

/// <summary>This installation of the app, as the push service knows it.</summary>
/// <param name="Platform">1 Android, 2 iOS, matching the server's PushPlatform.</param>
/// <param name="TimeZone">The device's IANA time zone id, so "06:00" means 06:00 where the seeker is.</param>
public sealed record PushDevice(int Platform, string Token, string? DeviceModel, string? AppVersion, string? TimeZone);

/// <summary>Obtains the device's push token from the platform's push service. One per host.</summary>
public interface IPushRegistrar
{
    /// <summary>
    /// The device's token, or null when this host cannot receive push at all: a browser, a phone
    /// without Google Play services, or a build that was made without the Firebase configuration
    /// file. Null is a normal answer, not an error; the app then keeps its own alarms.
    /// </summary>
    Task<PushDevice?> GetDeviceAsync();
}

/// <summary>What the web host gets: a browser tab is not a push target here.</summary>
public sealed class NoPushRegistrar : IPushRegistrar
{
    public Task<PushDevice?> GetDeviceAsync() => Task.FromResult<PushDevice?>(null);
}

/// <summary>
/// Carries "the seeker tapped a notification that opens this page" from the native shell, which
/// hears about the tap, to the Blazor layout, which is the only thing that can navigate.
/// </summary>
/// <remarks>
/// A singleton, like <see cref="IAppLifecycle"/>, and for the same reason: the activity that
/// receives the tap exists outside (and often before) the Blazor scope. A tap that starts the app
/// from cold arrives before any layout is listening, so the route is held until it is taken.
/// </remarks>
public sealed class DeepLinks
{
    private readonly object _gate = new();
    private string? _pending;

    /// <summary>Raised when a route arrives while a layout is already on screen.</summary>
    public event Action? Arrived;

    /// <summary>Raised when a push arrives while the app is open, so the bell can refetch its inbox.</summary>
    public event Action? Received;

    /// <summary>Raised when the push service replaces this installation's token, so it can be registered again.</summary>
    public event Action? TokenReplaced;

    /// <summary>Called by the native shell when a push has just been shown while the app is in front.</summary>
    public void MessageReceived() => Received?.Invoke();

    /// <summary>Called by the native shell when the push service issues a new token.</summary>
    public void TokenChanged() => TokenReplaced?.Invoke();

    /// <summary>Called by the native shell. Only in-app paths are accepted; anything else is dropped.</summary>
    public void Open(string? route)
    {
        if (!IsInAppPath(route)) return;

        lock (_gate) _pending = route;
        Arrived?.Invoke();
    }

    /// <summary>The waiting route, handed over once.</summary>
    public string? Take()
    {
        lock (_gate)
        {
            var route = _pending;
            _pending = null;
            return route;
        }
    }

    /// <summary>
    /// A single leading slash followed by a path. The route comes out of a push payload, so it is
    /// treated as untrusted: "//host" and "/\host" are how a path becomes another site.
    /// </summary>
    public static bool IsInAppPath(string? route)
        => !string.IsNullOrWhiteSpace(route)
           && route.Length <= 200
           && route[0] == '/'
           && (route.Length == 1 || (route[1] != '/' && route[1] != '\\'))
           && !route.Contains("://", StringComparison.Ordinal);
}
