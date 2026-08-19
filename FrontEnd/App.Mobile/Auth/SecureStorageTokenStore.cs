using App.Core.Auth;

namespace App.Mobile.Auth;

/// <summary>
/// Keeps the JWT in the platform credential store — the iOS Keychain, and on Android an
/// EncryptedSharedPreferences file whose key lives in the hardware-backed Keystore.
/// </summary>
/// <remarks>
/// Every operation is guarded. The keystore is not guaranteed to work: it is absent on some
/// emulator images, and on Android it can throw for a whole class of real-device reasons — a
/// corrupted keyset after a restore, a changed lock screen, or a vendor Keystore fault. Previously
/// only the read was guarded, so a write fault surfaced as an unhandled exception out of the
/// sign-in button, which in a Blazor Hybrid host tears down the WebView rather than showing an
/// error. Losing the credential store should cost the seeker a re-login, never the app.
/// </remarks>
public class SecureStorageTokenStore : ITokenStore
{
    private const string Key = "sc-token";

    /// <summary>
    /// Mirrors the stored value for the lifetime of the process.
    /// </summary>
    /// <remarks>
    /// BearerTokenHandler asks for the token on EVERY request, and each miss is a Keystore
    /// decryption — measurable work on a mid-range phone, and it happens six times on one dashboard
    /// load.
    /// <para>
    /// This only works because the store is registered as a SINGLETON. As a scoped service it
    /// would be a correctness bug, not an optimisation: IHttpClientFactory builds the handler
    /// pipeline in its own scope, so BearerTokenHandler would hold a different instance than
    /// AuthService — the login POST would latch "no token" on the handler's copy, and every request
    /// after signing in would go out unauthenticated until that handler rotated.
    /// </para>
    /// <para>
    /// Guarded by a lock because the pipeline is concurrent and phones are ARM, where a plain
    /// bool/reference pair can be observed out of order. The lock is uncontended in practice and
    /// still orders of magnitude cheaper than the Keystore read it replaces.
    /// </para>
    /// </remarks>
    private readonly object _gate = new();
    private string? _cached;
    private bool _loaded;

    public async Task<string?> GetTokenAsync()
    {
        lock (_gate)
        {
            if (_loaded) return _cached;
        }

        string? token;
        try { token = await SecureStorage.Default.GetAsync(Key); }
        catch { token = null; } // no keystore, or an unreadable keyset — treat as signed out

        lock (_gate)
        {
            // A concurrent Set/Clear may have won the race; never overwrite a newer value with the
            // one we just read from disk.
            if (!_loaded)
            {
                _cached = token;
                _loaded = true;
            }

            return _cached;
        }
    }

    public async Task SetTokenAsync(string token)
    {
        // Cache first so the session works even if persistence fails: the seeker stays signed in
        // for this run and is simply asked to sign in again next launch.
        lock (_gate)
        {
            _cached = token;
            _loaded = true;
        }

        try
        {
            await SecureStorage.Default.SetAsync(Key, token);
        }
        catch
        {
            // A device that cannot persist a credential is not a device that should crash here.
            // Clearing the (possibly half-written) entry keeps the next read honest.
            TryRemove();
        }
    }

    public Task ClearTokenAsync()
    {
        lock (_gate)
        {
            _cached = null;
            _loaded = true;
        }

        TryRemove();
        return Task.CompletedTask;
    }

    private static void TryRemove()
    {
        try { SecureStorage.Default.Remove(Key); }
        catch { /* nothing left to do — the in-memory copy is already gone */ }
    }
}
