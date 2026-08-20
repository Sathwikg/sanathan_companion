namespace Sanathana.Companion.Api.Configuration;

/// <summary>Settings for the four endpoints that stream bytes.</summary>
public class MediaOptions
{
    public const string SectionName = "Media";

    /// <summary>
    /// Whether a media URL must carry a valid ticket.
    /// </summary>
    /// <remarks>
    /// On by default: with it off the endpoints are world-readable and the ticket is pure cost. The
    /// switch exists so it can be turned off from configuration without a redeploy — a client that
    /// has not caught up, or a rollout that goes wrong, should not leave a site with no images.
    /// </remarks>
    public bool RequireTicket { get; set; } = true;
}
