using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Sanathana.Companion.Api.RateLimiting;

/// <summary>
/// Bucket key for the Panchangam compute endpoint: one budget per signed-in seeker.
/// </summary>
/// <remarks>
/// Address alone would put a whole carrier NAT into one bucket, for the reasons
/// <see cref="AuthRateLimitPartition"/> already sets out. This endpoint is behind [Authorize], so
/// the user id is the honest partition and the address only covers the sliver between the limiter
/// and the 401.
/// <para>
/// That only works because <c>UseRateLimiter</c> now runs AFTER <c>UseAuthentication</c>. In the
/// original order the limiter saw an anonymous principal on every request and silently fell all
/// the way back to the address — the exact single-bucket failure it was meant to avoid.
/// </para>
/// </remarks>
public static class ComputeRateLimitPartition
{
    public static string For(HttpContext context)
    {
        // MapInboundClaims is false, so "sub" arrives unmapped. The NameIdentifier fallback mirrors
        // CurrentUserService and survives a future change to that setting.
        var user = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                   ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return user is not null
            ? $"user:{user}"
            : $"addr:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }
}
