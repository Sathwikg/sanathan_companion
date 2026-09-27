namespace Sanathana.Companion.Application.Interfaces;

/// <summary>The two things a Google sign-in ticket carries between two requests.</summary>
public sealed record GoogleTicket(string GoogleSubject, string Email);

/// <summary>What a ticket may be spent on. Baked into the signature, so a register ticket cannot link and vice versa.</summary>
public static class GoogleTicketPurposes
{
    public const string Register = "register";
    public const string Link = "link";
}

/// <summary>
/// Signs and reads the short-lived ticket that carries a verified Google identity from the sign-in
/// call to the register or link call that follows it.
/// </summary>
/// <remarks>
/// Stateless on purpose: the ticket IS the proof that Google vouched for this subject and this email
/// a moment ago, so the follow-up request need not present the ID token again (which may have
/// expired by the time a seeker finishes typing a mobile number) and nothing has to be stored
/// between the two calls. Same construction as the media ticket: HMAC under a key derived from the
/// signing secret, so rotating that secret voids every ticket in flight.
/// </remarks>
public interface IGoogleTicketService
{
    (string Ticket, DateTime ExpiresAtUtc) Issue(string purpose, GoogleTicket payload, DateTime nowUtc);

    /// <summary>The payload, or null when the ticket is malformed, forged, expired or for another purpose.</summary>
    GoogleTicket? TryRead(string purpose, string? ticket, DateTime nowUtc);
}
