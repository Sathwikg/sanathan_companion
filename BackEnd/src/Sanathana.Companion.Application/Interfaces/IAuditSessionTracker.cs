namespace Sanathana.Companion.Application.Interfaces;

/// <summary>
/// Records the life of a sign-in. Called by the authentication service after each change has been
/// committed, so a sign-in that failed to save never appears as a session.
/// </summary>
public interface IAuditSessionTracker
{
    void Opened(Guid sessionId, Guid userId, string? email);
    void Heartbeat(Guid sessionId);
    void Closed(Guid sessionId, string exitReason);
    void ClosedForUser(Guid userId, string exitReason, Guid? exceptSessionId = null);
}
