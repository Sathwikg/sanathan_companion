namespace Sanathana.Companion.Application.Interfaces;

/// <summary>
/// Issues and checks the short-lived ticket that the media endpoints ask for.
/// </summary>
/// <remarks>
/// Those four endpoints stream bytes to an &lt;img&gt;, an &lt;audio&gt; or a download link, none of
/// which can carry a bearer token — so they are anonymous, and until now the URL alone was a
/// capability that never expired and could not be withdrawn. A ticket turns "forever" into "this
/// window and the last one".
/// <para>
/// The ticket is deliberately the same for every caller inside a window. That is what keeps the
/// URL cacheable: a per-user ticket would make every image a fresh cache entry for every seeker.
/// It is not an identity, and nothing here should be read as one.
/// </para>
/// </remarks>
public interface IMediaTicketService
{
    /// <summary>The ticket to hand a signed-in client, and when it stops being accepted.</summary>
    (string Ticket, DateTime ExpiresAtUtc) Issue(DateTime nowUtc);

    bool IsValid(string? ticket, DateTime nowUtc);

    /// <summary>How long a freshly-minted ticket is guaranteed to last.</summary>
    TimeSpan Window { get; }
}
