using System.Net;

namespace App.Core.Auth;

/// <summary>
/// Turns a module denial into a screen the seeker can read.
/// </summary>
/// <remarks>
/// Only a 403 carrying <c>X-Access-Denied: module</c> counts, and that is the whole design. Two
/// pages — the admin dashboard and the issue-types master — already inspect a plain 403 themselves
/// and render their own "administrator access required" panel; a handler that reacted to every 403
/// would navigate away while those pages were drawing their correct state. Role-based denials from
/// [Authorize(Roles = "Admin")] carry no such header, so they are left alone.
/// </remarks>
public class ForbiddenHandler : DelegatingHandler
{
    private readonly AccessDeniedNotifier _notifier;

    public ForbiddenHandler(AccessDeniedNotifier notifier) => _notifier = notifier;

    /// <summary>Set by the API's module filter; see ModuleAccessFilter.DeniedHeader.</summary>
    public const string DeniedHeader = "X-Access-Denied";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Forbidden
            && response.Headers.TryGetValues(DeniedHeader, out var values)
            && values.Any(v => string.Equals(v, "module", StringComparison.OrdinalIgnoreCase)))
        {
            _notifier.Raise();
        }

        return response;
    }
}
