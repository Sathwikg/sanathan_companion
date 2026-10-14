using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;
using Sanathana.Companion.Infrastructure.Persistence;

namespace Sanathana.Companion.Infrastructure.BackgroundJobs;

/// <summary>
/// Background job for cleaning up expired and stale data.
/// </summary>
/// <remarks>
/// This job runs periodically to clean up:
/// - Expired refresh tokens
/// - Old audit logs (beyond retention)
/// - Stale error logs
/// - Expired media tickets
/// - Abandoned user sessions
/// </remarks>
public sealed class CleanupJob
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<CleanupJob> _logger;

    public CleanupJob(ApplicationDbContext db, ILogger<CleanupJob> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Runs all cleanup tasks.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Summary of cleanup actions.</returns>
    public async Task<CleanupSummary> RunAllCleanupAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting cleanup job");

        var summary = new CleanupSummary();

        try
        {
            summary.ExpiredRefreshTokens = await CleanupExpiredRefreshTokensAsync(cancellationToken);
            summary.ExpiredMediaTickets = await CleanupExpiredMediaTicketsAsync(cancellationToken);
            summary.StaleSessions = await CleanupStaleSessionsAsync(cancellationToken);
            summary.OldErrorLogs = await CleanupOldErrorLogsAsync(cancellationToken);

            _logger.LogInformation(
                "Cleanup job completed: {Tokens} tokens, {Tickets} tickets, {Sessions} sessions, {Errors} errors removed",
                summary.ExpiredRefreshTokens, summary.ExpiredMediaTickets, summary.StaleSessions, summary.OldErrorLogs);

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cleanup job failed");
            throw;
        }
    }

    /// <summary>
    /// Removes expired refresh tokens.
    /// </summary>
    public async Task<int> CleanupExpiredRefreshTokensAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var expiredTokens = await _db.RefreshTokens
            .Where(t => t.ExpiresAtUtc < now || t.RevokedAtUtc != null)
            .ToListAsync(cancellationToken);

        if (expiredTokens.Count == 0) return 0;

        _db.RefreshTokens.RemoveRange(expiredTokens);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("Cleaned up {Count} expired/revoked refresh tokens", expiredTokens.Count);
        return expiredTokens.Count;
    }

    /// <summary>
    /// Removes expired media tickets (older than 12 hours - two 6-hour windows).
    /// </summary>
    public async Task<int> CleanupExpiredMediaTicketsAsync(CancellationToken cancellationToken = default)
    {
        // Media tickets are HMAC-based and don't have a database table.
        // They are validated statelessly. This is a placeholder for any
        // future ticket tracking if needed.
        await Task.CompletedTask;
        return 0;
    }

    /// <summary>
    /// Closes stale user sessions (no heartbeat for extended period).
    /// </summary>
    public async Task<int> CleanupStaleSessionsAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddHours(-24);
        var staleSessions = await _db.AuditUserSessions
            .Where(s => s.LogoutTimeUtc == null && s.LastHeartbeatUtc < cutoff)
            .ToListAsync(cancellationToken);

        if (staleSessions.Count == 0) return 0;

        foreach (var session in staleSessions)
        {
            session.LogoutTimeUtc = session.LastHeartbeatUtc ?? session.LoginTimeUtc;
            session.ExitReason = "Stale - No Heartbeat";
            session.DurationSeconds = (int)(session.LogoutTimeUtc.Value - session.LoginTimeUtc).TotalSeconds;
            _db.AuditUserSessions.Update(session);
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("Closed {Count} stale sessions", staleSessions.Count);
        return staleSessions.Count;
    }

    /// <summary>
    /// Removes error logs older than 30 days.
    /// </summary>
    public async Task<int> CleanupOldErrorLogsAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-30);
        var deletedCount = await _db.ErrorLogs
            .Where(e => e.TimestampUtc < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        if (deletedCount > 0)
        {
            _logger.LogDebug("Deleted {Count} old error logs", deletedCount);
        }

        return deletedCount;
    }

    /// <summary>
    /// Cleans up orphaned user favorites (referencing deleted entities).
    /// </summary>
    public async Task<int> CleanupOrphanedFavoritesAsync(CancellationToken cancellationToken = default)
    {
        // This would require checking each favorite type against its entity table
        // For now, placeholder
        await Task.CompletedTask;
        return 0;
    }
}

/// <summary>
/// Summary of cleanup job results.
/// </summary>
public sealed class CleanupSummary
{
    public int ExpiredRefreshTokens { get; set; }
    public int ExpiredMediaTickets { get; set; }
    public int StaleSessions { get; set; }
    public int OldErrorLogs { get; set; }
    public int OrphanedFavorites { get; set; }

    public int Total => ExpiredRefreshTokens + ExpiredMediaTickets + StaleSessions + OldErrorLogs + OrphanedFavorites;
}