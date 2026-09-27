using Google.Apis.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Infrastructure.Identity;

/// <inheritdoc />
/// <remarks>
/// Google's own library does the work: it fetches Google's current signing keys (cached for as long
/// as Google's cache headers allow), checks the signature, the issuer, the expiry and, given the
/// audience list, that the token was minted for one of THIS app's OAuth clients. That last check is
/// the one people forget, and without it any site's Google token would open an account here.
/// </remarks>
public sealed class GoogleIdTokenVerifier : IGoogleIdTokenVerifier
{
    private readonly GoogleSignInOptions _options;
    private readonly ILogger<GoogleIdTokenVerifier> _logger;

    public GoogleIdTokenVerifier(IOptions<GoogleSignInOptions> options, ILogger<GoogleIdTokenVerifier> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<GoogleIdentity?> VerifyAsync(string idToken, CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            // Not an error worth a 500: the feature is simply off on this deployment. The client
            // only shows the button when it has a client id, so this is reached by hand-crafted
            // requests or a half-configured environment, and a warning is the right volume.
            _logger.LogWarning("A Google sign-in was attempted but Google:ClientIds is empty, so it was refused.");
            return null;
        }

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = _options.ClientIds.Where(c => !string.IsNullOrWhiteSpace(c)).ToArray()
            });

            if (string.IsNullOrWhiteSpace(payload.Subject) || string.IsNullOrWhiteSpace(payload.Email))
                return null;

            return new GoogleIdentity(payload.Subject, payload.Email, payload.EmailVerified, payload.Name);
        }
        catch (InvalidJwtException ex)
        {
            // Expired, wrong audience, bad signature, not from Google. Logged at Information
            // because a stale token from a phone that slept is the usual cause, not an attack.
            _logger.LogInformation("A Google ID token was rejected: {Reason}", ex.Message);
            return null;
        }
    }
}
