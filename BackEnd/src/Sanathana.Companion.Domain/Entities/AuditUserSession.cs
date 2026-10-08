namespace Sanathana.Companion.Domain.Entities;

/// <summary>
/// One sign-in, from the moment tokens were issued to the moment the session ended.
/// </summary>
/// <remarks>
/// <see cref="Id"/> is the refresh-token family id, not a fresh guid. Every token rotated from one
/// sign-in shares that family, so the family is the session: refreshes are its heartbeat, and the
/// family's revocation (sign-out, password change, reuse, a closed account) or expiry is its end.
/// The access token carries the same id as its "sid" claim, which is how page visits are tied to
/// the session they happened in.
/// </remarks>
public class AuditUserSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? UserId { get; set; }
    public string? UsernameOrEmail { get; set; }

    public DateTime LoginTimeUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LogoutTimeUtc { get; set; }
    public int? DurationSeconds { get; set; }

    /// <summary>One of <see cref="AuditExitReasons"/>. Null while the session is open.</summary>
    public string? ExitReason { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Platform { get; set; }

    /// <summary>The last time the session was seen alive: a token refresh or a page visit.</summary>
    public DateTime? LastHeartbeatUtc { get; set; }
}

/// <summary>Why a session ended. Stored as text so the log stays readable without a lookup.</summary>
public static class AuditExitReasons
{
    public const string ExplicitLogout = "ExplicitLogout";
    public const string SessionExpired = "SessionExpired";
    public const string PasswordChanged = "PasswordChanged";
    public const string ReuseDetected = "ReuseDetected";
    public const string AccountDisabled = "AccountDisabled";

    /// <summary>Maps a refresh token's revocation reason onto the session's exit reason.</summary>
    public static string FromRevocation(string? revokedReason) => revokedReason switch
    {
        "signed-out" => ExplicitLogout,
        "password-changed" => PasswordChanged,
        "reuse-detected" => ReuseDetected,
        "disabled" => AccountDisabled,
        _ => SessionExpired
    };
}
