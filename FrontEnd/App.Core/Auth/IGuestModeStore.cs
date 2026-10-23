namespace App.Core.Auth;

/// <summary>
/// Remembers that this device chose "Continue as Guest" (web = localStorage, mobile = Preferences).
/// </summary>
/// <remarks>
/// Persisted so a guest who closes the app comes back to the home screen rather than the login
/// page. It is only a preference, never a credential: a guest sends no token, and what they may
/// read is decided by the API against the Guest role. Signing in or out clears it.
/// </remarks>
public interface IGuestModeStore
{
    Task<bool> IsGuestAsync();

    Task SetGuestAsync(bool isGuest);
}
