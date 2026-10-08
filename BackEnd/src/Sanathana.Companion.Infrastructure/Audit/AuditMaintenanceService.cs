using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Infrastructure.Persistence;

namespace Sanathana.Companion.Infrastructure.Audit;

/// <summary>
/// The housekeeping the audit log needs and no request triggers.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Every minute: reload the switches, so a second API instance follows what was saved on the first.</item>
/// <item>Every 15 minutes: close sessions that ended without a sign-out (expiry, a closed account,
/// a revoked token family), using the refresh-token table as the source of truth.</item>
/// <item>Once a day: delete rows older than the retention the administrator set.</item>
/// </list>
/// Each step catches its own failures, so a bad night for one never stops the others.
/// </remarks>
public sealed class AuditMaintenanceService : BackgroundService
{
    public static readonly TimeSpan CacheReloadInterval = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan SessionSweepInterval = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan PurgeInterval = TimeSpan.FromHours(24);

    /// <summary>A session this young may still be in the queue's hands; leave it alone.</summary>
    private static readonly TimeSpan SweepGrace = TimeSpan.FromMinutes(5);
    private const int SweepBatch = 500;

    /// <summary>The floor applies even if the database holds less, so a typo cannot wipe the log.</summary>
    private const int MinRetentionDays = 7;

    private readonly IAuditConfigCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditMaintenanceService> _logger;

    public AuditMaintenanceService(IAuditConfigCache cache, IServiceScopeFactory scopeFactory, ILogger<AuditMaintenanceService> logger)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var nextSweep = DateTime.UtcNow.AddMinutes(2);
        var nextPurge = DateTime.UtcNow.AddMinutes(10);
        using var timer = new PeriodicTimer(CacheReloadInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await _cache.ReloadAsync(stoppingToken);

                var now = DateTime.UtcNow;
                if (now >= nextSweep)
                {
                    await RunSafelyAsync("close ended sessions", ct => SweepSessionsAsync(now, ct), stoppingToken);
                    nextSweep = now + SessionSweepInterval;
                }

                if (now >= nextPurge)
                {
                    await RunSafelyAsync("purge expired audit rows", ct => PurgeAsync(now, ct), stoppingToken);
                    nextPurge = now + PurgeInterval;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    /// <summary>
    /// Closes open sessions whose refresh-token family has no live token left. The family's last
    /// token says how it ended: revoked for a reason, or simply expired.
    /// </summary>
    /// <returns>How many sessions were closed.</returns>
    public async Task<int> SweepSessionsAsync(DateTime nowUtc, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var cutoff = nowUtc - SweepGrace;
        var open = await db.AuditUserSessions
            .Where(s => s.LogoutTimeUtc == null && s.LoginTimeUtc < cutoff)
            .OrderBy(s => s.LoginTimeUtc)
            .Take(SweepBatch)
            .ToListAsync(ct);
        if (open.Count == 0) return 0;

        var ids = open.Select(s => s.Id).ToList();
        var tokens = (await db.RefreshTokens.AsNoTracking()
                .Where(t => ids.Contains(t.FamilyId))
                .Select(t => new { t.FamilyId, t.CreatedDate, t.ExpiresAtUtc, t.RevokedAtUtc, t.RevokedReason })
                .ToListAsync(ct))
            .ToLookup(t => t.FamilyId);

        var closed = 0;
        foreach (var session in open)
        {
            var family = tokens[session.Id].ToList();
            if (family.Any(t => t.RevokedAtUtc is null && t.ExpiresAtUtc > nowUtc)) continue; // still signed in

            var last = family.OrderByDescending(t => t.CreatedDate).FirstOrDefault();
            if (last?.RevokedAtUtc is { } revokedAt)
            {
                AuditSessionCloser.Close(session, AuditExitReasons.FromRevocation(last.RevokedReason), revokedAt);
            }
            else
            {
                // Expired, or the tokens are gone with a deleted account. The session really ended
                // when it was last seen, not when the token ran out weeks later.
                AuditSessionCloser.Close(session, AuditExitReasons.SessionExpired, session.LastHeartbeatUtc ?? session.LoginTimeUtc);
            }
            closed++;
        }

        if (closed > 0) await db.SaveChangesAsync(ct);
        return closed;
    }

    /// <summary>Deletes rows past their retention. Set-based, so it never loads what it removes.</summary>
    public async Task PurgeAsync(DateTime nowUtc, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var settings = await db.AuditSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (settings is null) return;

        var auditCutoff = nowUtc.AddDays(-Math.Max(MinRetentionDays, settings.AuditRetentionDays));
        var errorCutoff = nowUtc.AddDays(-Math.Max(MinRetentionDays, settings.ErrorRetentionDays));

        var activities = await db.AuditActivityLogs.Where(a => a.EnteredAtUtc < auditCutoff).ExecuteDeleteAsync(ct);
        var dataLogs = await db.AuditDataLogs.Where(d => d.TimestampUtc < auditCutoff).ExecuteDeleteAsync(ct);
        // Open sessions are kept whatever their age: they are still somebody's live sign-in.
        var sessions = await db.AuditUserSessions.Where(s => s.LogoutTimeUtc != null && s.LogoutTimeUtc < auditCutoff).ExecuteDeleteAsync(ct);
        var errors = await db.ErrorLogs.Where(e => e.TimestampUtc < errorCutoff).ExecuteDeleteAsync(ct);

        if (activities + dataLogs + sessions + errors > 0)
        {
            _logger.LogInformation(
                "Audit retention removed {Activities} page visits, {DataLogs} data changes, {Sessions} sessions and {Errors} errors.",
                activities, dataLogs, sessions, errors);
        }
    }

    private async Task RunSafelyAsync(string what, Func<CancellationToken, Task> step, CancellationToken ct)
    {
        try
        {
            await step(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audit maintenance could not {What}; it will try again on the next run.", what);
        }
    }
}
