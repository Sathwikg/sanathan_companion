namespace Sanathana.Companion.Application.Interfaces;

/// <summary>
/// Mints and hashes refresh tokens, and decides how long one lives.
/// </summary>
/// <remarks>
/// The lifetime lives here rather than on JwtSettings because JwtSettings is an Infrastructure
/// type and this layer cannot see it.
/// </remarks>
public interface IRefreshTokenFactory
{
    /// <summary>A fresh token: the value to hand the client, and the hash to store.</summary>
    (string Token, string Hash) Create();

    /// <summary>The stored form of a token the client presented.</summary>
    string Hash(string token);

    DateTime ExpiresAt(DateTime nowUtc);
}
