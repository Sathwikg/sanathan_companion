using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Infrastructure.Identity;

/// <inheritdoc />
public class RefreshTokenFactory : IRefreshTokenFactory
{
    private readonly JwtSettings _settings;
    private readonly IAuditRequestContext? _request;

    /// <param name="request">The current request, to tell the phone app from a browser. Absent outside a request (tests), which reads as a browser.</param>
    public RefreshTokenFactory(IOptions<JwtSettings> options, IAuditRequestContext? request = null)
    {
        _settings = options.Value;
        _request = request;
    }

    // Hex rather than base64 so nothing downstream has to be URL- or JSON-escaped; the same
    // shape AuthRateLimitPartition already uses for its fingerprints.
    public (string Token, string Hash) Create()
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        return (token, Hash(token));
    }

    public string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <remarks>
    /// The header is the client's own claim and cannot be verified. That is acceptable here: the
    /// most a forged "Android" buys is a browser session that lasts as long as a phone's, which is
    /// still revoked by signing out, by a password change and by reuse detection.
    /// </remarks>
    public DateTime ExpiresAt(DateTime nowUtc) => nowUtc.AddDays(IsPhoneApp ? _settings.MobileRefreshTokenDays : _settings.RefreshTokenDays);

    private bool IsPhoneApp => AuditPlatforms.Normalize(_request?.Platform) is "Android" or "iOS";
}
