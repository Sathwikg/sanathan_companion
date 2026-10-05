using System.Threading.Channels;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Infrastructure.Audit;

/// <summary>
/// The in-memory hand-off between request threads and <see cref="AuditBatchProcessor"/>.
/// </summary>
/// <remarks>
/// Bounded so a stalled database cannot grow it without limit. When full, the oldest record goes:
/// recent activity is the more useful half during an incident. Each drop is counted so the
/// processor can say in the log that records were lost, rather than losing them silently.
/// </remarks>
public sealed class AuditQueue : IAuditQueue
{
    public const int Capacity = 10_000;

    private readonly Channel<object> _channel;
    private long _dropped;

    public AuditQueue()
    {
        var options = new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        };
        _channel = Channel.CreateBounded<object>(options, _ => Interlocked.Increment(ref _dropped));
    }

    public ChannelReader<object> Reader => _channel.Reader;

    public long DroppedCount => Interlocked.Read(ref _dropped);

    public void EnqueueSessionOpened(AuditUserSession session) => Write(session);
    public void EnqueueSessionHeartbeat(Guid sessionId, DateTime atUtc) => Write(new SessionHeartbeat(sessionId, atUtc));
    public void EnqueueSessionClosed(Guid sessionId, string exitReason, DateTime atUtc) => Write(new SessionClose(sessionId, exitReason, atUtc));

    public void EnqueueSessionsClosedForUser(Guid userId, string exitReason, DateTime atUtc, Guid? exceptSessionId = null)
        => Write(new UserSessionsClose(userId, exitReason, atUtc, exceptSessionId));

    public void EnqueueActivity(AuditActivityLog activity) => Write(activity);
    public void EnqueueDataLog(AuditDataLog dataLog) => Write(dataLog);
    public void EnqueueError(ErrorLog errorLog) => Write(errorLog);

    /// <summary>Lets shutdown finish draining what is already queued.</summary>
    public void Complete() => _channel.Writer.TryComplete();

    private void Write(object item) => _channel.Writer.TryWrite(item);
}

/// <summary>The session was seen alive at this time (a token refresh or a page visit).</summary>
public sealed record SessionHeartbeat(Guid SessionId, DateTime AtUtc);

/// <summary>The session ended for this reason at this time.</summary>
public sealed record SessionClose(Guid SessionId, string ExitReason, DateTime AtUtc);

/// <summary>Every open session of the user ended, except the named one.</summary>
public sealed record UserSessionsClose(Guid UserId, string ExitReason, DateTime AtUtc, Guid? ExceptSessionId);
