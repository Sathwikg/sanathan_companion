using App.Core.Auth;

namespace App.Mobile.Auth;

/// <summary>
/// Keeps the tokens in the platform credential store — the iOS Keychain, and on Android an
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
    private const string AccessKey = "sc-token";
    private const string RefreshKey = "sc-refresh";

    /// <summary>
    /// Mirrors a stored value for the lifetime of the process.
    /// </summary>
    /// <remarks>
    /// BearerTokenHandler asks for the access token on EVERY request, and each miss is a Keystore
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
    private sealed class Cached
    {
        private readonly object _gate = new();
        private string? _value;
        private bool _loaded;

        public bool TryRead(out string? value)
        {
            lock (_gate)
            {
                value = _value;
                return _loaded;
            }
        }

        /// <summary>Fills the cache from disk, unless a concurrent write already won the race.</summary>
        public string? Seed(string? fromStore)
        {
            lock (_gate)
            {
                if (!_loaded)
                {
                    _value = fromStore;
                    _loaded = true;
                }

                return _value;
            }
        }

        public void Set(string? value)
        {
            lock (_gate)
            {
                _value = value;
                _loaded = true;
            }
        }
    }

    private readonly Cached _access = new();
    private readonly Cached _refresh = new();

    public Task<string?> GetTokenAsync() => ReadAsync(AccessKey, _access);

    public Task<string?> GetRefreshTokenAsync() => ReadAsync(RefreshKey, _refresh);

    public async Task SetTokensAsync(string accessToken, string refreshToken)
    {
        // Cache first so the session works even if persistence fails: the seeker stays signed in
        // for this run and is simply asked to sign in again next launch.
        _access.Set(accessToken);
        _refresh.Set(refreshToken);

        await WriteAsync(AccessKey, accessToken);
        await WriteAsync(RefreshKey, refreshToken);
    }

    public Task ClearTokenAsync()
    {
        _access.Set(null);
        _refresh.Set(null);

        TryRemove(AccessKey);
        TryRemove(RefreshKey);
        return Task.CompletedTask;
    }

    private static async Task<string?> ReadAsync(string key, Cached cache)
    {
        if (cache.TryRead(out var cached)) return cached;

        string? stored;
        try { stored = await SecureStorage.Default.GetAsync(key); }
        catch { stored = null; } // no keystore, or an unreadable keyset — treat as signed out

        return cache.Seed(stored);
    }

    private static async Task WriteAsync(string key, string value)
    {
        try
        {
            await SecureStorage.Default.SetAsync(key, value);
        }
        catch
        {
            // A device that cannot persist a credential is not a device that should crash here.
            // Clearing the (possibly half-written) entry keeps the next read honest.
            TryRemove(key);
        }
    }

    private static void TryRemove(string key)
    {
        try { SecureStorage.Default.Remove(key); }
        catch { /* nothing left to do — the in-memory copy is already gone */ }
    }
}
