namespace Sanathana.Companion.Application.Interfaces;

/// <summary>
/// Decides whether an access token that is cryptographically valid is still <em>allowed</em>.
/// </summary>
/// <remarks>
/// A signed JWT proves who minted it and when it expires, and nothing else. It cannot know that
/// the account was closed ten minutes ago, or that the seeker changed their password because they
/// thought someone had it. That is a database question, so it is asked once per authenticated
/// request — uncached, because a revocation nobody notices for five minutes is not a revocation.
/// </remarks>
public interface ITokenValidityService
{
    /// <summary>
    /// True when the account is open and the token was minted at or after the account's cut-off.
    /// </summary>
    /// <param name="issuedAtUtc">The token's <c>nbf</c>, which JwtTokenService stamps on every token.</param>
    Task<bool> IsStillValidAsync(Guid userId, DateTime issuedAtUtc, CancellationToken cancellationToken = default);
}
