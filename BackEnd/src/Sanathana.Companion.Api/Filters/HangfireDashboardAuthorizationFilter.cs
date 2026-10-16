using Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;

namespace Sanathana.Companion.Api.Filters;

/// <summary>
/// Authorization filter for Hangfire Dashboard - restricts access to Admin role.
/// </summary>
public sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    private readonly bool _requireAdmin;

    public HangfireDashboardAuthorizationFilter(bool requireAdmin = true)
    {
        _requireAdmin = requireAdmin;
    }

    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // Allow in development without auth
        if (IsDevelopmentEnvironment(httpContext))
        {
            return true;
        }

        // Check if user is authenticated
        if (httpContext.User?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        // Check for Admin role
        if (_requireAdmin)
        {
            return httpContext.User.IsInRole("Admin");
        }

        return true;
    }

    private static bool IsDevelopmentEnvironment(HttpContext httpContext)
    {
        var env = httpContext.RequestServices.GetService(typeof(IWebHostEnvironment)) as IWebHostEnvironment;
        return env?.IsDevelopment() == true;
    }
}