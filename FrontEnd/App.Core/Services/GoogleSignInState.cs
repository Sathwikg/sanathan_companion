namespace App.Core.Services;

/// <summary>
/// Carries the ticket from a first Google sign-in to the page that finishes it — the registration
/// form for a new address, or the password prompt for an existing account.
/// </summary>
/// <remarks>
/// In memory only, and scoped, so it lives exactly as long as the app session: a ticket is proof
/// that Google vouched for an address a moment ago, and it has no business in localStorage or a
/// query string, where it would outlive the moment and land in history and logs. A page that finds
/// it empty or expired sends the seeker back to the login screen to start again.
/// </remarks>
public class GoogleSignInState
{
    public string? Ticket { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }
    public string? Email { get; private set; }
    public string? FullName { get; private set; }

    public bool IsUsable => !string.IsNullOrWhiteSpace(Ticket) && ExpiresAtUtc > DateTime.UtcNow;

    public void Set(string ticket, DateTime? expiresAtUtc, string? email, string? fullName)
    {
        Ticket = ticket;
        // Absent from the response means "trust the server's default" rather than "already dead".
        ExpiresAtUtc = expiresAtUtc ?? DateTime.UtcNow.AddMinutes(10);
        Email = email;
        FullName = fullName;
    }

    public void Clear()
    {
        Ticket = null;
        ExpiresAtUtc = null;
        Email = null;
        FullName = null;
    }
}
