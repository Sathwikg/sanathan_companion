using App.Core.Auth;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace App.Core.Services;

/// <summary>
/// Sends a guest to sign in, and brings them back to the page they were on afterwards.
/// </summary>
/// <remarks>
/// Two ways in. A members-only PAGE (the japa screen, Favorites, Profile…) is handled by the router:
/// <c>RedirectToLogin</c> builds the same URL from the address the guest tried to open. A
/// members-only ACTION on a page a guest may see (a favourite heart, a wallpaper download) calls
/// <see cref="EnsureSignedInAsync"/> before doing anything.
/// </remarks>
public class LoginGate
{
    private readonly NavigationManager _nav;
    private readonly AuthenticationStateProvider _auth;

    public LoginGate(NavigationManager nav, AuthenticationStateProvider auth)
    {
        _nav = nav;
        _auth = auth;
    }

    /// <summary>
    /// True for a signed-in seeker. Anyone else is sent to the login page, which returns them here
    /// once they have signed in; the caller should then stop.
    /// </summary>
    public async Task<bool> EnsureSignedInAsync()
    {
        var state = await _auth.GetAuthenticationStateAsync();
        if (GuestSession.IsMember(state.User)) return true;

        _nav.NavigateTo(LoginUrlForHere());
        return false;
    }

    /// <summary>The login page, set to come back to the current page.</summary>
    public string LoginUrlForHere() => LoginUrl(_nav.ToBaseRelativePath(_nav.Uri));

    /// <summary>The registration page, set to come back to the current page.</summary>
    public string RegisterUrlForHere() => RegisterUrl(_nav.ToBaseRelativePath(_nav.Uri));

    public static string LoginUrl(string? returnUrl) => WithReturn("login", returnUrl);

    public static string RegisterUrl(string? returnUrl) => WithReturn("register", returnUrl);

    private static string WithReturn(string page, string? returnUrl)
    {
        var safe = SafeReturnUrl(returnUrl);
        return safe.Length == 0 ? page : $"{page}?returnUrl={Uri.EscapeDataString(safe)}";
    }

    /// <summary>
    /// Where to go after signing in: a path inside this app, base-relative, or "" for home.
    /// </summary>
    /// <remarks>
    /// The value arrives in the query string, so anybody can write a link that sets it. Only an
    /// in-app path survives: an absolute URL, a scheme ("javascript:"), a protocol-relative
    /// "//host" or a backslash form of one would otherwise turn the login page into a redirect to
    /// somebody else's site. The auth pages themselves are dropped too, so signing in can never
    /// land on the login page again.
    /// </remarks>
    public static string SafeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl)) return string.Empty;

        var url = returnUrl.Trim();
        if (url.Any(char.IsControl) || url.Contains('\\')) return string.Empty;
        if (url.StartsWith("//", StringComparison.Ordinal)) return string.Empty;

        // A scheme is a colon before the path, query or fragment begins.
        var end = url.IndexOfAny(new[] { '/', '?', '#' });
        if (url.IndexOf(':', 0, end < 0 ? url.Length : end) >= 0) return string.Empty;

        url = url.TrimStart('/');

        var path = url.Split('?', '#')[0].TrimEnd('/');
        if (path.Equals("login", StringComparison.OrdinalIgnoreCase)
            || path.Equals("register", StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        return url;
    }
}
