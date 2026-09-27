namespace Sanathana.Companion.Application.Interfaces;

/// <summary>What a verified Google ID token says about the person who signed in.</summary>
/// <param name="Subject">Google's stable account id (<c>sub</c>). The only thing an account is ever matched on once linked.</param>
/// <param name="Email">The address as Google holds it; not yet normalised.</param>
/// <param name="EmailVerified">Google's own flag. False for a handful of legacy account types, and then the address proves nothing.</param>
/// <param name="Name">Display name, when the profile scope granted one.</param>
public sealed record GoogleIdentity(string Subject, string Email, bool EmailVerified, string? Name);

/// <summary>
/// Checks a Google ID token's signature, issuer, expiry and audience, and returns who it is for.
/// </summary>
/// <remarks>
/// An Application interface with an Infrastructure implementation, like <c>IPasswordHasher</c>: the
/// real one talks to Google's public keys, and the tests hand the service a fake that says whatever
/// the test needs. Returns null for anything that does not verify. The caller answers 401 and
/// never learns why, which is deliberate.
/// </remarks>
public interface IGoogleIdTokenVerifier
{
    Task<GoogleIdentity?> VerifyAsync(string idToken, CancellationToken cancellationToken = default);
}
