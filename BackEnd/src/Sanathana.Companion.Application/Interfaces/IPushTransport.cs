namespace Sanathana.Companion.Application.Interfaces;

/// <summary>Whether a push can actually leave the building right now, and if not, why.</summary>
/// <param name="Enabled">The PushNotifications:Enabled switch.</param>
/// <param name="Ready">Enabled, credentials found and accepted: a send will be attempted.</param>
/// <param name="ProjectId">The Firebase project the credentials belong to, for the console.</param>
/// <param name="Reason">What is missing, in words an administrator can act on. Null when ready.</param>
public sealed record PushTransportStatus(bool Enabled, bool Ready, string? ProjectId, string? Reason);

/// <summary>What a notification says and how the phone should present it.</summary>
/// <param name="Kind">The notification type's code; the app uses it to pick artwork and actions.</param>
/// <param name="Channel">The Android channel id (see NotificationChannels).</param>
/// <param name="Route">The in-app page a tap opens.</param>
/// <param name="Tag">Messages with the same tag replace one another in the shade instead of stacking.</param>
/// <param name="Ttl">
/// How long the push service keeps trying a phone that is off or out of signal. Short for anything
/// about a moment: a Rahu Kalam warning delivered that evening is worse than none.
/// </param>
public sealed record PushMessage(
    string Title,
    string Body,
    string Kind,
    string Channel,
    string? Route = null,
    string? ImageUrl = null,
    string? Tag = null,
    TimeSpan? Ttl = null);

/// <summary>One message addressed to one device.</summary>
public sealed record PushEnvelope(Guid DeviceId, string Token, int Platform, PushMessage Message);

/// <summary>What the push service said about one envelope.</summary>
/// <param name="TokenDead">The token is unregistered or malformed and will never work again.</param>
public sealed record PushSendOutcome(Guid DeviceId, bool Ok, bool TokenDead, string? Error);

/// <summary>
/// The wire to the phones (Firebase Cloud Messaging). Deliberately knows nothing about seekers,
/// preferences or schedules: it is handed finished messages and reports what happened to each.
/// </summary>
public interface IPushTransport
{
    PushTransportStatus Status { get; }

    /// <summary>Sends every envelope and returns one outcome per envelope. Never throws for a refused message.</summary>
    Task<IReadOnlyList<PushSendOutcome>> SendAsync(IReadOnlyList<PushEnvelope> envelopes, CancellationToken cancellationToken = default);
}
