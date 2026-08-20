using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Sanathana.Companion.Api.Configuration;

/// <summary>
/// Reads the instant a bearer token was minted, from the token itself.
/// </summary>
/// <remarks>
/// From the CLAIM, not by casting the validated token object. The first version of the revocation
/// check did <c>context.SecurityToken is JwtSecurityToken jwt ? jwt.ValidFrom : DateTime.UtcNow</c>,
/// and on this stack the handler produces a <c>JsonWebToken</c> instead — so the cast always failed,
/// the fallback said "now", and every token passed the cut-off comparison forever. It looked
/// correct, compiled, and revoked nothing. Reading the claim works whichever handler validated it,
/// and returning null lets the caller fail closed.
/// </remarks>
public static class TokenIssuedAt
{
    public static DateTime? From(ClaimsPrincipal? principal)
    {
        var notBefore = principal?.FindFirst(JwtRegisteredClaimNames.Nbf)?.Value;

        return long.TryParse(notBefore, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime
            : null;
    }
}
