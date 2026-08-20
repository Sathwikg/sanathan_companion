using System.Security.Cryptography;
using System.Text;

namespace Sanathana.Companion.Api.RateLimiting;

/// <summary>Builds the bucket key for the auth endpoints' rate limiter.</summary>
/// <remarks>
/// Keyed on the caller's address AND the credential being tried, because either alone is wrong:
/// <list type="bullet">
/// <item>
/// Address alone puts everyone behind one carrier NAT, office egress or — before
/// UseForwardedHeaders — the entire internet into a single bucket, so ten bad attempts locked out
/// every seeker.
/// </item>
/// <item>
/// Credential alone lets an attacker spray one password across thousands of accounts unthrottled.
/// </item>
/// </list>
/// The credential is hashed before it becomes a key: partition keys end up in memory, in dumps and
/// potentially in diagnostics, and an e-mail address or mobile number is personal data that has no
/// reason to be sitting in any of them.
/// </remarks>
public static class AuthRateLimitPartition
{
    public static string For(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var credential = ReadCredential(context);

        return credential is null ? address : $"{address}|{Fingerprint(credential)}";
    }

    /// <summary>
    /// The credential from the request, when it is already buffered and cheap to read.
    /// </summary>
    /// <remarks>
    /// Returns null rather than reading the body: the limiter runs before model binding, and
    /// consuming the stream here would leave nothing for the endpoint. Callers that want the finer
    /// bucket can send the header; everyone else is still throttled on address alone, which is no
    /// worse than before.
    /// </remarks>
    private static string? ReadCredential(HttpContext context)
    {
        // Set by the client on sign-in so the two can be throttled apart. Absent is fine.
        if (context.Request.Headers.TryGetValue("X-Auth-Subject", out var header))
        {
            var value = header.ToString();
            if (!string.IsNullOrWhiteSpace(value) && value.Length <= 320)
                return value.Trim().ToLowerInvariant();
        }

        return null;
    }

    private static string Fingerprint(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));

        // 8 bytes is ample to separate buckets; the full digest would only make the key longer.
        return Convert.ToHexString(bytes.AsSpan(0, 8));
    }
}
