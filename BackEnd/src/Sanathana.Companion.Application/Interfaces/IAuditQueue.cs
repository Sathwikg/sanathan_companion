using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Application.Interfaces;

/// <summary>
/// Hands audit records to a background writer so no request waits on, or fails because of, the
/// audit tables. Every method is non-blocking and never throws.
/// </summary>
/// <remarks>
/// The queue is bounded. When it is full the oldest record is dropped rather than stalling the
/// caller; <see cref="DroppedCount"/> says how often that has happened, and the writer logs it.
/// </remarks>
public interface IAuditQueue
{
    void EnqueueSessionOpened(AuditUserSession session);
    void EnqueueSessionHeartbeat(Guid sessionId, DateTime atUtc);
    void EnqueueSessionClosed(Guid sessionId, string exitReason, DateTime atUtc);

    /// <summary>Closes every open session of a user, except the one named (the session that asked).</summary>
    void EnqueueSessionsClosedForUser(Guid userId, string exitReason, DateTime atUtc, Guid? exceptSessionId = null);

    void EnqueueActivity(AuditActivityLog activity);
    void EnqueueDataLog(AuditDataLog dataLog);
    void EnqueueError(ErrorLog errorLog);

    /// <summary>Records discarded because the queue was full, since start-up.</summary>
    long DroppedCount { get; }
}
