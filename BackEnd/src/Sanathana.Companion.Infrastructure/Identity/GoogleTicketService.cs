using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Infrastructure.Identity;

/// <inheritdoc />
/// <remarks>
/// The ticket is <c>base64url(purpose|subject|email|expiresUnix) . base64url(HMAC-SHA256)</c>. The
/// payload travels in the clear because nothing in it is secret to the person holding it (it is
/// their own address), and keeping it readable means the API can answer "expired" precisely. The
/// MAC is what stops anyone minting a ticket for an address Google never vouched for.
/// <para>
/// The key is derived from the JWT secret with HKDF and its own label, like the media ticket, so
/// the three HMAC users of that secret can never be replayed against one another.
/// </para>
/// </remarks>
public sealed class GoogleTicketService : IGoogleTicketService
{
    private const char Separator = '|';

    private readonly byte[] _key;
    private readonly GoogleSignInOptions _options;

    public GoogleTicketService(IOptions<JwtSettings> jwt, IOptions<GoogleSignInOptions> options)
    {
        _options = options.Value;
        _key = HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            ikm: Encoding.UTF8.GetBytes(jwt.Value.Secret),
            outputLength: 32,
            info: Encoding.UTF8.GetBytes("sc-google-ticket-v1"));
    }

    public (string Ticket, DateTime ExpiresAtUtc) Issue(string purpose, GoogleTicket payload, DateTime nowUtc)
    {
        var expiresAt = nowUtc.AddMinutes(_options.TicketMinutes);
        var expiresUnix = new DateTimeOffset(DateTime.SpecifyKind(expiresAt, DateTimeKind.Utc)).ToUnixTimeSeconds();

        // The separator cannot occur in a subject (digits) or an email address, and the purpose is
        // one of two constants, so a plain join is unambiguous.
        var body = string.Join(Separator, purpose, payload.GoogleSubject, payload.Email, expiresUnix.ToString());
        var bodyBytes = Encoding.UTF8.GetBytes(body);

        return ($"{Base64Url(bodyBytes)}.{Base64Url(HMACSHA256.HashData(_key, bodyBytes))}", expiresAt);
    }

    public GoogleTicket? TryRead(string purpose, string? ticket, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(ticket)) return null;

        var dot = ticket.IndexOf('.');
        if (dot <= 0 || dot == ticket.Length - 1) return null;

        byte[] bodyBytes, presented;
        try
        {
            bodyBytes = FromBase64Url(ticket[..dot]);
            presented = FromBase64Url(ticket[(dot + 1)..]);
        }
        catch (FormatException)
        {
            return null;
        }

        // Signature first, then contents: nothing below is worth parsing on a forged ticket.
        var expected = HMACSHA256.HashData(_key, bodyBytes);
        if (presented.Length != expected.Length || !CryptographicOperations.FixedTimeEquals(expected, presented))
            return null;

        var parts = Encoding.UTF8.GetString(bodyBytes).Split(Separator);
        if (parts.Length != 4) return null;
        if (!string.Equals(parts[0], purpose, StringComparison.Ordinal)) return null;
        if (!long.TryParse(parts[3], out var expiresUnix)) return null;

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expiresUnix).UtcDateTime;
        if (expiresAt <= nowUtc) return null;

        if (string.IsNullOrWhiteSpace(parts[1]) || string.IsNullOrWhiteSpace(parts[2])) return null;

        return new GoogleTicket(parts[1], parts[2]);
    }

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }
}
