using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Infrastructure.Persistence;

namespace Sanathana.Companion.Infrastructure.Audit;

/// <summary>
/// Drains <see cref="AuditQueue"/> into the database in batches.
/// </summary>
/// <remarks>
/// <para>
/// A batch is written when it is full or when the oldest record in it has waited
/// <see cref="FlushInterval"/>, whichever comes first, so a lone record never sits in memory
/// waiting for company.
/// </para>
/// <para>
/// A failed batch is never retried as a whole. It is replayed one record at a time and any record
/// that still fails is logged and dropped, so one bad row (a value the column cannot hold, a
/// duplicate key) costs that row and nothing else, instead of wedging the queue until restart.
/// </para>
/// </remarks>
public sealed class AuditBatchProcessor : BackgroundService
{
    public const int BatchSize = 100;
    public static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ShutdownDrainBudget = TimeSpan.FromSeconds(5);

    private readonly AuditQueue _queue;
    private readonly IAuditConfigCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditBatchProcessor> _logger;
    private long _reportedDrops;

    public AuditBatchProcessor(
        AuditQueue queue,
        IAuditConfigCache cache,
        IServiceScopeFactory scopeFactory,
        ILogger<AuditBatchProcessor> logger)
    {
        _queue = queue;
        _cache = cache;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        // Before the first request is served. Without this the switches stayed at their compiled
        // defaults (everything on) until an administrator happened to open the audit screen.
        // Never throws: a failure is logged and the defaults stand until the next periodic reload.
        await _cache.EnsureInitializedAsync(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = _queue.Reader;
        var buffer = new List<object>(BatchSize);

        try
        {
            while (await reader.WaitToReadAsync(stoppingToken))
            {
                var deadline = DateTime.UtcNow + FlushInterval;

                while (buffer.Count < BatchSize)
                {
                    while (buffer.Count < BatchSize && reader.TryRead(out var item))
                        buffer.Add(item);

                    var remaining = deadline - DateTime.UtcNow;
                    if (buffer.Count >= BatchSize || remaining <= TimeSpan.Zero) break;

                    using var wait = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    wait.CancelAfter(remaining);
                    try
                    {
                        if (!await reader.WaitToReadAsync(wait.Token)) break;
                    }
                    catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                    {
                        break; // the deadline passed: write what we have
                    }
                }

                await ProcessBatchAsync(buffer, stoppingToken);
                buffer.Clear();
                ReportDrops();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down; fall through and drain.
        }

        await DrainOnShutdownAsync(buffer);
    }

    /// <summary>
    /// Writes one batch, isolating failures to the records that cause them. Never throws.
    /// Public so tests can drive it without the timing loop.
    /// </summary>
    public async Task ProcessBatchAsync(IReadOnlyList<object> items, CancellationToken ct)
    {
        if (items.Count == 0) return;

        try
        {
            await WriteAsync(items, ct);
            return;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (items.Count > 1)
        {
            _logger.LogWarning(ex, "Audit batch of {Count} records failed; retrying them one at a time.", items.Count);
        }
        catch (Exception ex)
        {
            LogDropped(ex, items[0]);
            return;
        }

        foreach (var item in items)
        {
            try
            {
                await WriteAsync(new[] { item }, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogDropped(ex, item);
            }
        }
    }

    private async Task WriteAsync(IReadOnlyList<object> items, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var heartbeats = new Dictionary<Guid, DateTime>();
        var closes = new List<SessionClose>();
        var userCloses = new List<UserSessionsClose>();

        foreach (var item in items)
        {
            switch (item)
            {
                case AuditUserSession s:
                    AuditColumnFitter.Fit(db, s);
                    db.AuditUserSessions.Add(s);
                    break;
                case AuditActivityLog a:
                    AuditColumnFitter.Fit(db, a);
                    db.AuditActivityLogs.Add(a);
                    // A page visit is proof of life for its session.
                    if (a.SessionId is { } sid) Bump(heartbeats, sid, a.ExitedAtUtc ?? a.EnteredAtUtc);
                    break;
                case AuditDataLog d:
                    AuditColumnFitter.Fit(db, d);
                    db.AuditDataLogs.Add(d);
                    break;
                case ErrorLog e:
                    AuditColumnFitter.Fit(db, e);
                    db.ErrorLogs.Add(e);
                    break;
                case SessionHeartbeat h:
                    Bump(heartbeats, h.SessionId, h.AtUtc);
                    break;
                case SessionClose c:
                    closes.Add(c);
                    break;
                case UserSessionsClose u:
                    userCloses.Add(u);
                    break;
            }
        }

        // Inserts first, so a session opened and closed within one batch is found by its close.
        await db.SaveChangesAsync(ct);

        if (heartbeats.Count == 0 && closes.Count == 0 && userCloses.Count == 0) return;

        var ids = heartbeats.Keys.Concat(closes.Select(c => c.SessionId)).Distinct().ToList();
        var sessions = ids.Count == 0
            ? new Dictionary<Guid, AuditUserSession>()
            : await db.AuditUserSessions.Where(s => ids.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);

        foreach (var (id, at) in heartbeats)
        {
            if (sessions.TryGetValue(id, out var s) && s.LogoutTimeUtc is null
                && (s.LastHeartbeatUtc is null || at > s.LastHeartbeatUtc))
            {
                s.LastHeartbeatUtc = at;
            }
        }

        foreach (var c in closes)
        {
            if (sessions.TryGetValue(c.SessionId, out var s))
                AuditSessionCloser.Close(s, c.ExitReason, c.AtUtc);
        }

        foreach (var u in userCloses)
        {
            var open = await db.AuditUserSessions
                .Where(s => s.UserId == u.UserId && s.LogoutTimeUtc == null && s.Id != u.ExceptSessionId)
                .ToListAsync(ct);
            foreach (var s in open)
                AuditSessionCloser.Close(s, u.ExitReason, u.AtUtc);
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task DrainOnShutdownAsync(List<object> buffer)
    {
        _queue.Complete();
        while (_queue.Reader.TryRead(out var item)) buffer.Add(item);
        if (buffer.Count == 0) return;

        using var budget = new CancellationTokenSource(ShutdownDrainBudget);
        try
        {
            for (var i = 0; i < buffer.Count; i += BatchSize)
                await ProcessBatchAsync(buffer.Skip(i).Take(BatchSize).ToList(), budget.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Shutdown drain of the audit queue ran out of time; some records were not written.");
        }
    }

    private void ReportDrops()
    {
        var dropped = _queue.DroppedCount;
        if (dropped == _reportedDrops) return;

        _logger.LogWarning(
            "The audit queue was full and discarded {New} record(s) ({Total} since start-up). The database is not keeping up.",
            dropped - _reportedDrops, dropped);
        _reportedDrops = dropped;
    }

    private void LogDropped(Exception ex, object item)
        => _logger.LogError(ex, "Dropped an audit record of type {Type} that could not be written.", item.GetType().Name);

    private static void Bump(Dictionary<Guid, DateTime> map, Guid id, DateTime at)
    {
        if (!map.TryGetValue(id, out var current) || at > current) map[id] = at;
    }
}

/// <summary>The one place a session row is closed, so every path computes the duration the same way.</summary>
public static class AuditSessionCloser
{
    public static void Close(AuditUserSession session, string exitReason, DateTime atUtc)
    {
        if (session.LogoutTimeUtc is not null) return;

        // Never before it began: a heartbeat can be stamped earlier than the login on a skewed clock.
        if (atUtc < session.LoginTimeUtc) atUtc = session.LoginTimeUtc;

        session.LogoutTimeUtc = atUtc;
        session.ExitReason = exitReason;
        session.DurationSeconds = (int)Math.Min(int.MaxValue, (atUtc - session.LoginTimeUtc).TotalSeconds);
        if (session.LastHeartbeatUtc is null || atUtc > session.LastHeartbeatUtc)
            session.LastHeartbeatUtc = atUtc;
    }
}
