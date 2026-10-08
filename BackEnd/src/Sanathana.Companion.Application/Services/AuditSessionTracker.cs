using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Application.Services;

public sealed class AuditSessionTracker : IAuditSessionTracker
{
    private readonly IAuditQueue _queue;
    private readonly IAuditConfigCache _cache;
    private readonly IAuditRequestContext _request;

    public AuditSessionTracker(IAuditQueue queue, IAuditConfigCache cache, IAuditRequestContext request)
    {
        _queue = queue;
        _cache = cache;
        _request = request;
    }

    public void Opened(Guid sessionId, Guid userId, string? email)
    {
        if (!_cache.TrackUserSessions) return;

        var now = DateTime.UtcNow;
        _queue.EnqueueSessionOpened(new AuditUserSession
        {
            Id = sessionId,
            UserId = userId,
            UsernameOrEmail = email,
            LoginTimeUtc = now,
            LastHeartbeatUtc = now,
            IpAddress = _request.IpAddress,
            UserAgent = _request.UserAgent,
            Platform = _request.Platform
        });
    }

    // Heartbeats and closes are sent even with session tracking paused: they only ever update a row
    // that already exists, and a session opened before the pause should still get its ending.
    public void Heartbeat(Guid sessionId) => _queue.EnqueueSessionHeartbeat(sessionId, DateTime.UtcNow);

    public void Closed(Guid sessionId, string exitReason) => _queue.EnqueueSessionClosed(sessionId, exitReason, DateTime.UtcNow);

    public void ClosedForUser(Guid userId, string exitReason, Guid? exceptSessionId = null)
        => _queue.EnqueueSessionsClosedForUser(userId, exitReason, DateTime.UtcNow, exceptSessionId);
}
