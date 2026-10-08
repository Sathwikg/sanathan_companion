using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Infrastructure.Audit;

namespace Sanathana.Companion.Tests;

/// <summary>Keeps every record in a list, in the order it was enqueued.</summary>
internal sealed class RecordingAuditQueue : IAuditQueue
{
    public List<object> Items { get; } = new();

    public IEnumerable<T> OfType<T>() => Items.OfType<T>();

    public long DroppedCount => 0;

    public void EnqueueSessionOpened(AuditUserSession session) => Items.Add(session);
    public void EnqueueSessionHeartbeat(Guid sessionId, DateTime atUtc) => Items.Add(new SessionHeartbeat(sessionId, atUtc));
    public void EnqueueSessionClosed(Guid sessionId, string exitReason, DateTime atUtc) => Items.Add(new SessionClose(sessionId, exitReason, atUtc));

    public void EnqueueSessionsClosedForUser(Guid userId, string exitReason, DateTime atUtc, Guid? exceptSessionId = null)
        => Items.Add(new UserSessionsClose(userId, exitReason, atUtc, exceptSessionId));

    public void EnqueueActivity(AuditActivityLog activity) => Items.Add(activity);
    public void EnqueueDataLog(AuditDataLog dataLog) => Items.Add(dataLog);
    public void EnqueueError(ErrorLog errorLog) => Items.Add(errorLog);
}

/// <summary>Switches a test sets directly. Per-form rules default to on, as in the real cache.</summary>
internal sealed class FakeAuditConfigCache : IAuditConfigCache
{
    public bool IsGlobalAuditEnabled { get; set; } = true;
    public bool Sessions { get; set; } = true;
    public bool Navigation { get; set; } = true;
    public bool Data { get; set; } = true;
    public bool TrackErrorLogs { get; set; } = true;

    public Dictionary<string, bool> ActivityByModule { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, bool> DataByModule { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, AuditRoute> Routes { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool TrackUserSessions => IsGlobalAuditEnabled && Sessions;
    public bool TrackPageNavigation => IsGlobalAuditEnabled && Navigation;
    public bool TrackDataModifications => IsGlobalAuditEnabled && Data;

    public bool IsActivityAuditEnabled(string? moduleCode)
        => TrackPageNavigation && (moduleCode is null || !ActivityByModule.TryGetValue(moduleCode, out var on) || on);

    public bool IsDataAuditEnabled(string? moduleCode)
        => TrackDataModifications && (moduleCode is null || !DataByModule.TryGetValue(moduleCode, out var on) || on);

    public AuditRoute? ResolveRoute(string? routePath)
        => routePath is not null && Routes.TryGetValue(routePath.Trim('/'), out var route) ? route : null;

    public int Reloads { get; private set; }

    public Task EnsureInitializedAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task ReloadAsync(CancellationToken ct = default)
    {
        Reloads++;
        return Task.CompletedTask;
    }
}

internal sealed class FakeAuditRequestContext : IAuditRequestContext
{
    public bool IsHttpRequest { get; set; } = true;
    public string? IpAddress { get; set; } = "203.0.113.7";
    public string? UserAgent { get; set; } = "test-agent";
    public string? Platform { get; set; } = "Web";
    public string? Endpoint { get; set; } = "PUT /api/test";
    public string? ModuleCode { get; set; }
    public Guid? SessionId { get; set; }
}
