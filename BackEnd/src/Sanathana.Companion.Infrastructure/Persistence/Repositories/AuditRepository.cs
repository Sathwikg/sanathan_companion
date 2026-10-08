using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Infrastructure.Persistence.Repositories;

public class AuditRepository : IAuditRepository
{
    private readonly ApplicationDbContext _context;

    public AuditRepository(ApplicationDbContext context) => _context = context;

    public Task<AuditSettings?> GetSettingsAsync(CancellationToken ct = default)
        => _context.AuditSettings.FirstOrDefaultAsync(ct);

    public async Task AddSettingsAsync(AuditSettings settings, CancellationToken ct = default)
        => await _context.AuditSettings.AddAsync(settings, ct);

    public Task<List<AuditModuleConfig>> GetModuleConfigsAsync(CancellationToken ct = default)
        => _context.AuditModuleConfigs.ToListAsync(ct);

    public async Task AddModuleConfigAsync(AuditModuleConfig config, CancellationToken ct = default)
        => await _context.AuditModuleConfigs.AddAsync(config, ct);

    public Task<AuditPage<AuditUserSession>> QuerySessionsAsync(AuditLogFilter f, CancellationToken ct = default)
    {
        var q = _context.AuditUserSessions.AsNoTracking();
        if (f.FromUtc is { } from) q = q.Where(s => s.LoginTimeUtc >= from);
        if (f.ToUtc is { } to) q = q.Where(s => s.LoginTimeUtc < to);
        if (Term(f) is { } t)
        {
            q = q.Where(s => (s.UsernameOrEmail != null && s.UsernameOrEmail.ToLower().Contains(t))
                          || (s.IpAddress != null && s.IpAddress.ToLower().Contains(t))
                          || (s.Platform != null && s.Platform.ToLower().Contains(t))
                          || (s.ExitReason != null && s.ExitReason.ToLower().Contains(t)));
        }
        return PageAsync(q.OrderByDescending(s => s.LoginTimeUtc), f, ct);
    }

    public Task<AuditPage<AuditActivityLog>> QueryActivitiesAsync(AuditLogFilter f, CancellationToken ct = default)
    {
        var q = _context.AuditActivityLogs.AsNoTracking();
        if (f.FromUtc is { } from) q = q.Where(a => a.EnteredAtUtc >= from);
        if (f.ToUtc is { } to) q = q.Where(a => a.EnteredAtUtc < to);
        if (Term(f) is { } t)
        {
            q = q.Where(a => (a.UsernameOrEmail != null && a.UsernameOrEmail.ToLower().Contains(t))
                          || a.ModuleCode.ToLower().Contains(t)
                          || (a.FormName != null && a.FormName.ToLower().Contains(t))
                          || a.RoutePath.ToLower().Contains(t));
        }
        return PageAsync(q.OrderByDescending(a => a.EnteredAtUtc), f, ct);
    }

    public Task<AuditPage<AuditDataLog>> QueryDataLogsAsync(AuditLogFilter f, CancellationToken ct = default)
    {
        var q = _context.AuditDataLogs.AsNoTracking();
        if (f.FromUtc is { } from) q = q.Where(d => d.TimestampUtc >= from);
        if (f.ToUtc is { } to) q = q.Where(d => d.TimestampUtc < to);
        if (f.Action is { } action) q = q.Where(d => d.Action == action);
        if (Term(f) is { } t)
        {
            q = q.Where(d => (d.UsernameOrEmail != null && d.UsernameOrEmail.ToLower().Contains(t))
                          || d.EntityName.ToLower().Contains(t)
                          || (d.EntityId != null && d.EntityId.ToLower().Contains(t))
                          || (d.ModuleCode != null && d.ModuleCode.ToLower().Contains(t))
                          || (d.ChangedColumns != null && d.ChangedColumns.ToLower().Contains(t)));
        }
        return PageAsync(q.OrderByDescending(d => d.TimestampUtc), f, ct);
    }

    public Task<AuditPage<ErrorLog>> QueryErrorsAsync(AuditLogFilter f, CancellationToken ct = default)
    {
        var q = _context.ErrorLogs.AsNoTracking();
        if (f.FromUtc is { } from) q = q.Where(e => e.TimestampUtc >= from);
        if (f.ToUtc is { } to) q = q.Where(e => e.TimestampUtc < to);
        if (f.Source is { } source) q = q.Where(e => e.Source == source);
        if (f.Resolved is { } resolved) q = q.Where(e => e.IsResolved == resolved);
        if (Term(f) is { } t)
        {
            q = q.Where(e => e.Message.ToLower().Contains(t)
                          || e.ExceptionType.ToLower().Contains(t)
                          || (e.RequestPath != null && e.RequestPath.ToLower().Contains(t))
                          || (e.UsernameOrEmail != null && e.UsernameOrEmail.ToLower().Contains(t)));
        }
        return PageAsync(q.OrderByDescending(e => e.TimestampUtc), f, ct);
    }

    public Task<ErrorLog?> GetErrorByIdAsync(Guid id, CancellationToken ct = default)
        => _context.ErrorLogs.FirstOrDefaultAsync(e => e.Id == id, ct);

    private static string? Term(AuditLogFilter f)
        => string.IsNullOrWhiteSpace(f.Search) ? null : f.Search.Trim().ToLowerInvariant();

    private static async Task<AuditPage<T>> PageAsync<T>(IOrderedQueryable<T> query, AuditLogFilter f, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await query.Skip(f.Skip).Take(f.Take).ToListAsync(ct);
        return new AuditPage<T>(items, total);
    }
}
