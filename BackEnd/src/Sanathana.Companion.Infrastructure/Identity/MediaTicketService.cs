using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Infrastructure.Identity;

/// <inheritdoc />
public sealed class MediaTicketService : IMediaTicketService
{
    /// <summary>
    /// Six hours. The length is a trade, not a security parameter to minimise.
    /// </summary>
    /// <remarks>
    /// The ticket sits in the query string, so it is part of the HTTP cache key — every rotation
    /// costs a full re-download of every visible image, and these are phones. Thirty minutes would
    /// mean doing that forty-eight times a day. The property being bought is "bounded rather than
    /// forever", and six hours buys it at four rotations.
    /// </remarks>
    private const int WindowSeconds = 6 * 60 * 60;

    private readonly byte[] _key;

    public MediaTicketService(IOptions<JwtSettings> options)
    {
        // Derived from the signing secret rather than reusing it: the same key doing two jobs is
        // how one of them ends up weakening the other. HKDF is in the shared framework.
        _key = HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            ikm: Encoding.UTF8.GetBytes(options.Value.Secret),
            outputLength: 32,
            info: Encoding.UTF8.GetBytes("sc-media-ticket-v1"));
    }

    public TimeSpan Window => TimeSpan.FromSeconds(WindowSeconds);

    public (string Ticket, DateTime ExpiresAtUtc) Issue(DateTime nowUtc)
    {
        var bucket = BucketOf(nowUtc);

        // Two buckets, not one: a ticket minted at 05:59 into a window would otherwise die a minute
        // later, and the client would hand the WebView a dead ticket it had just fetched.
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds((bucket + 2) * WindowSeconds).UtcDateTime;

        return ($"{bucket}.{Sign(bucket)}", expiresAt);
    }

    public bool IsValid(string? ticket, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(ticket)) return false;

        var dot = ticket.IndexOf('.');
        if (dot <= 0 || dot == ticket.Length - 1) return false;

        if (!long.TryParse(ticket[..dot], out var bucket)) return false;

        var current = BucketOf(nowUtc);
        if (bucket != current && bucket != current - 1) return false;

        var expected = Encoding.UTF8.GetBytes(Sign(bucket));
        var presented = Encoding.UTF8.GetBytes(ticket[(dot + 1)..]);

        return CryptographicOperations.FixedTimeEquals(expected, presented);
    }

    private static long BucketOf(DateTime nowUtc)
        => new DateTimeOffset(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc)).ToUnixTimeSeconds() / WindowSeconds;

    private string Sign(long bucket)
    {
        var mac = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(bucket.ToString()));

        // Sixteen bytes is ample for a value that is only good for two windows, and it keeps the
        // URL short. Base64url so nothing downstream has to escape it.
        return Convert.ToBase64String(mac.AsSpan(0, 16))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
