using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.Common.Authorization;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Api.Filters;

/// <summary>
/// Enforces Access Rights at the endpoint, not just in the menu.
/// </summary>
/// <remarks>
/// Until this existed, the role-by-module matrix decided only which links a seeker saw. Every API
/// endpoint was gated by nothing finer than [Authorize], so a role denied the Deities form could
/// still read <c>GET /api/deities</c> by asking for it. The writes were already closed by
/// [Authorize(Roles = "Admin")]; it is the reads this shuts.
/// <para>
/// Default-deny is the point: an authenticated non-Admin reaching an endpoint that names no module
/// is refused, so a controller added tomorrow is closed until somebody maps it. Administrators are
/// exempt before that check, so a forgotten attribute can never brick administration.
/// </para>
/// <para>
/// Guests are the one anonymous caller it gates. On an endpoint marked [GuestAccess] a token-less
/// request is checked against the Guest role's row in the same matrix, so what a guest may browse
/// is an Access Rights decision like any other — and an endpoint nobody marked stays closed to them.
/// </para>
/// </remarks>
public sealed class ModuleAccessFilter : IAsyncAuthorizationFilter
{
    private readonly IAccessRightsCatalog _catalog;

    public ModuleAccessFilter(IAccessRightsCatalog catalog) => _catalog = catalog;

    /// <summary>Marks a 403 as coming from THIS filter rather than from a role check.</summary>
    /// <remarks>
    /// Two screens already read a plain 403 and render their own "administrator access required"
    /// panel. Without a way to tell the two apart, the client's global handler would talk over
    /// them. Role-based denials carry no such header.
    /// </remarks>
    public const string DeniedHeader = "X-Access-Denied";

    /// <summary>The body a guest is refused with; the client sends them to sign in.</summary>
    public const string GuestDeniedMessage = "Please sign in to continue.";

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var metadata = context.ActionDescriptor.EndpointMetadata;
        var guestAccess = metadata.OfType<GuestAccessAttribute>().Any();

        // The public endpoints stay public. A guest endpoint is "anonymous" only so that [Authorize]
        // lets a token-less caller reach this filter; it is gated below.
        if (!guestAccess && metadata.OfType<IAllowAnonymous>().Any()) return;

        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            // A missing or expired token must stay a 401 from the authorization middleware and
            // never become a 403 — the client tells an ended session from a refused one by exactly
            // that. Only a guest endpoint lets an anonymous caller get this far.
            if (guestAccess) await AuthorizeGuestAsync(context, metadata);
            return;
        }

        // Administrators reach every form; the matrix has never stored a row for them.
        if (context.HttpContext.User.IsInRole(RoleNames.Admin)) return;

        if (metadata.OfType<ModuleExemptAttribute>().Any()) return;

        // Last, not first: MVC lists controller metadata before action metadata, so this is how an
        // action overrides its controller.
        var required = metadata.OfType<RequiresModuleAttribute>().LastOrDefault();
        if (required is not null)
        {
            var snapshot = await _catalog.GetAsync(context.HttpContext.RequestAborted);
            var role = context.HttpContext.User.FindFirst(ClaimTypes.Role)?.Value;
            if (snapshot.Allows(role, required.Codes)) return;
        }

        Deny(context);
    }

    /// <summary>
    /// A token-less caller on a [GuestAccess] endpoint: held to the Guest role's grants.
    /// </summary>
    private async Task AuthorizeGuestAsync(AuthorizationFilterContext context, IList<object> metadata)
    {
        // A request that PRESENTED a credential the server rejected is a session that has ended,
        // not a guest. Answering it as a guest would hand a signed-in seeker the guest view — no
        // counts, no streak — and swallow the 401 their client renews the token on.
        if (!string.IsNullOrEmpty(context.HttpContext.Request.Headers.Authorization))
        {
            context.Result = new ChallengeResult();
            return;
        }

        // The menu and the media ticket: what every screen a guest may see needs to draw itself.
        if (metadata.OfType<ModuleExemptAttribute>().Any()) return;

        var required = metadata.OfType<RequiresModuleAttribute>().LastOrDefault();
        if (required is not null)
        {
            var snapshot = await _catalog.GetAsync(context.HttpContext.RequestAborted);
            if (snapshot.Allows(RoleNames.Guest, required.Codes)) return;
        }

        // 401, not 403: what a guest lacks is an account, and the remedy is to sign in. It also
        // keeps the client's access-denied redirect, which is a signed-in page, out of the way.
        context.Result = JsonError(StatusCodes.Status401Unauthorized, GuestDeniedMessage);
    }

    private static void Deny(AuthorizationFilterContext context)
    {
        context.HttpContext.Response.Headers[DeniedHeader] = "module";
        context.Result = JsonError(StatusCodes.Status403Forbidden, "You do not have access to this form.");
    }

    /// <summary>
    /// The same body shape ExceptionHandlingMiddleware emits, so the client's error extraction
    /// keeps working. A ForbidResult would send an empty body and the seeker would see nothing.
    /// ContentResult rather than ObjectResult so the translation filter does not walk it.
    /// </summary>
    private static ContentResult JsonError(int statusCode, string message) => new()
    {
        StatusCode = statusCode,
        ContentType = "application/json",
        Content = System.Text.Json.JsonSerializer.Serialize(new
        {
            statusCode,
            message,
            timestamp = DateTime.UtcNow
        })
    };
}
