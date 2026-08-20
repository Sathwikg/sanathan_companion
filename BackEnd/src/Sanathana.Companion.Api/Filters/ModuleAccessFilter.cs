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

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var metadata = context.ActionDescriptor.EndpointMetadata;

        // The public endpoints stay public.
        if (metadata.OfType<IAllowAnonymous>().Any()) return;

        // A missing or expired token must stay a 401 from the authorization middleware and never
        // become a 403 — the client tells an ended session from a refused one by exactly that.
        if (context.HttpContext.User.Identity?.IsAuthenticated != true) return;

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

    private static void Deny(AuthorizationFilterContext context)
    {
        context.HttpContext.Response.Headers[DeniedHeader] = "module";

        // The same body shape ExceptionHandlingMiddleware emits, so the client's error extraction
        // keeps working. A ForbidResult would send an empty body and the seeker would see nothing.
        // ContentResult rather than ObjectResult so the translation filter does not walk it.
        context.Result = new ContentResult
        {
            StatusCode = StatusCodes.Status403Forbidden,
            ContentType = "application/json",
            Content = System.Text.Json.JsonSerializer.Serialize(new
            {
                statusCode = StatusCodes.Status403Forbidden,
                message = "You do not have access to this form.",
                timestamp = DateTime.UtcNow
            })
        };
    }
}
