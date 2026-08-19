namespace App.Core.Auth;

/// <summary>
/// Raised when the API rejects the stored credential, so the app can end the session instead of
/// carrying on as if it were still signed in.
/// </summary>
/// <remarks>
/// Registered as a SINGLETON, deliberately. <c>IHttpClientFactory</c> resolves delegating handlers
/// from its own scope, so a scoped notifier would give <see cref="SessionExpiryHandler"/> a
/// different instance than the one the router subscribes to and the event would never arrive —
/// the same trap already documented for <c>LanguageContext</c> in AddAppCore.
/// </remarks>
public class SessionExpiredNotifier
{
    private int _raised;

    /// <summary>Fired once per expiry. Subscribers should sign the user out and route to login.</summary>
    public event Action? Expired;

    /// <summary>
    /// Signals that the session is over. Collapses a burst into one event: a single screen can have
    /// six requests in flight, and each would otherwise fire its own sign-out and navigation.
    /// </summary>
    public void Raise()
    {
        if (Interlocked.Exchange(ref _raised, 1) == 1) return;
        Expired?.Invoke();
    }

    /// <summary>Re-arms after a successful sign-in, so the next expiry is heard.</summary>
    public void Reset() => Interlocked.Exchange(ref _raised, 0);
}
