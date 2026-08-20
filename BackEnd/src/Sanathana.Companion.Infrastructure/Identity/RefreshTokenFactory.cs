using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Infrastructure.Identity;

/// <inheritdoc />
public class RefreshTokenFactory : IRefreshTokenFactory
{
    private readonly JwtSettings _settings;

    public RefreshTokenFactory(IOptions<JwtSettings> options) => _settings = options.Value;

    // Hex rather than base64 so nothing downstream has to be URL- or JSON-escaped; the same
    // shape AuthRateLimitPartition already uses for its fingerprints.
    public (string Token, string Hash) Create()
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        return (token, Hash(token));
    }

    public string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public DateTime ExpiresAt(DateTime nowUtc) => nowUtc.AddDays(_settings.RefreshTokenDays);
}
