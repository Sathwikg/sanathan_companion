using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace App.Core.Auth;

/// <summary>
/// How the client tells a guest from a signed-in seeker, and the policy that lets a guest in.
/// </summary>
/// <remarks>
/// A guest is deliberately NOT authenticated: their principal carries one marker claim and no
/// authentication type. So every plain <c>[Authorize]</c> page and every
/// <c>&lt;AuthorizeView&gt;&lt;Authorized&gt;</c> block stays members-only without being touched —
/// the profile link, sign-out, the notification bell, the japa screen. Only a page or block that
/// asks for <see cref="BrowsePolicy"/> opens to a guest, so a page added tomorrow is closed to
/// them until somebody decides otherwise.
/// </remarks>
public static class GuestSession
{
    /// <summary>The marker claim on a guest's principal.</summary>
    public const string GuestClaim = "guest";

    /// <summary>Signed-in seekers and guests alike: the pages that only show shared content.</summary>
    public const string BrowsePolicy = "browse";

    public static ClaimsPrincipal CreatePrincipal()
        => new(new ClaimsIdentity(new[] { new Claim(GuestClaim, "true") }));

    public static bool IsGuest(ClaimsPrincipal? user)
        => user?.Identity?.IsAuthenticated != true && user?.HasClaim(c => c.Type == GuestClaim) == true;

    public static bool IsMember(ClaimsPrincipal? user) => user?.Identity?.IsAuthenticated == true;

    public static void AddPolicies(AuthorizationOptions options)
        => options.AddPolicy(BrowsePolicy, policy =>
            policy.RequireAssertion(ctx => IsMember(ctx.User) || IsGuest(ctx.User)));
}
