using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Infrastructure.Persistence;

namespace Sanathana.Companion.Infrastructure.Audit;

/// <summary>
/// Holds the audit switches and the route-to-form map as one immutable snapshot.
/// </summary>
/// <remarks>
/// A reload builds a complete new snapshot and swaps it in with a single reference assignment, so a
/// reader on another thread sees either the old configuration or the new one, never a half-cleared
/// dictionary that answers "default" for a form an administrator switched off.
/// </remarks>
public sealed class AuditConfigCache : IAuditConfigCache
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditConfigCache> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private volatile bool _initialized;
    private volatile Snapshot _snapshot = Snapshot.Defaults;

    public AuditConfigCache(IServiceScopeFactory scopeFactory, ILogger<AuditConfigCache> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public bool IsGlobalAuditEnabled => _snapshot.Global;
    public bool TrackUserSessions => _snapshot.Global && _snapshot.Sessions;
    public bool TrackPageNavigation => _snapshot.Global && _snapshot.Navigation;
    public bool TrackDataModifications => _snapshot.Global && _snapshot.Data;
    public bool TrackErrorLogs => _snapshot.Errors;

    public bool IsActivityAuditEnabled(string? moduleCode)
    {
        var snapshot = _snapshot;
        if (!(snapshot.Global && snapshot.Navigation)) return false;
        return string.IsNullOrWhiteSpace(moduleCode)
               || !snapshot.ActivityByModule.TryGetValue(moduleCode, out var enabled)
               || enabled;
    }

    public bool IsDataAuditEnabled(string? moduleCode)
    {
        var snapshot = _snapshot;
        if (!(snapshot.Global && snapshot.Data)) return false;
        return string.IsNullOrWhiteSpace(moduleCode)
               || !snapshot.DataByModule.TryGetValue(moduleCode, out var enabled)
               || enabled;
    }

    public AuditRoute? ResolveRoute(string? routePath)
    {
        var path = Normalize(routePath);

        // Longest match wins, so "/puja-process-config/x" is not credited to "/puja-process". The
        // root route only ever matches the root itself; as a prefix it would swallow every page.
        AuditRoute? best = null;
        var bestLength = -1;
        foreach (var (route, target) in _snapshot.Routes)
        {
            var matches = route.Length == 0
                ? path.Length == 0
                : path == route || path.StartsWith(route + "/", StringComparison.Ordinal);
            if (matches && route.Length > bestLength)
            {
                best = target;
                bestLength = route.Length;
            }
        }
        return best;
    }

    public async Task EnsureInitializedAsync(CancellationToken ct = default)
    {
        if (_initialized) return;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_initialized) return;
            await ReloadAsync(ct);
        }
        finally
        {
            // Set even after a failure: the periodic reload retries, and every caller retrying on
            // its own would turn a database outage into a stampede.
            _initialized = true;
            _initLock.Release();
        }
    }

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var settings = await db.AuditSettings.AsNoTracking().FirstOrDefaultAsync(ct);
            var configs = await db.AuditModuleConfigs.AsNoTracking()
                .Select(c => new { c.ModuleCode, c.IsActivityAuditEnabled, c.IsDataAuditEnabled })
                .ToListAsync(ct);
            var forms = await db.MenuModules.AsNoTracking()
                .Where(m => m.Code != null && m.Code != "" && m.RoutePath != null)
                .Select(m => new { m.Code, m.Name, m.RoutePath })
                .ToListAsync(ct);

            var routes = new Dictionary<string, AuditRoute>(StringComparer.Ordinal);
            foreach (var form in forms)
                routes.TryAdd(Normalize(form.RoutePath), new AuditRoute(form.Code!, form.Name));

            _snapshot = new Snapshot(
                Global: settings?.IsGlobalAuditEnabled ?? true,
                Sessions: settings?.TrackUserSessions ?? true,
                Navigation: settings?.TrackPageNavigation ?? true,
                Data: settings?.TrackDataModifications ?? true,
                Errors: settings?.TrackErrorLogs ?? true,
                ActivityByModule: configs
                    .Where(c => !string.IsNullOrWhiteSpace(c.ModuleCode))
                    .GroupBy(c => c.ModuleCode, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().IsActivityAuditEnabled, StringComparer.OrdinalIgnoreCase),
                DataByModule: configs
                    .Where(c => !string.IsNullOrWhiteSpace(c.ModuleCode))
                    .GroupBy(c => c.ModuleCode, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().IsDataAuditEnabled, StringComparer.OrdinalIgnoreCase),
                Routes: routes);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // The previous snapshot stays in force; an outage must not flip auditing on or off.
            _logger.LogWarning(ex, "Could not reload the audit configuration; keeping the previous settings.");
        }
    }

    /// <summary>"/Deities/123/" → "deities/123". Query and fragment are dropped.</summary>
    private static string Normalize(string? routePath)
    {
        if (string.IsNullOrWhiteSpace(routePath)) return string.Empty;
        var path = routePath.Split('?', '#')[0];
        return path.Trim().Trim('/').ToLowerInvariant();
    }

    private sealed record Snapshot(
        bool Global,
        bool Sessions,
        bool Navigation,
        bool Data,
        bool Errors,
        IReadOnlyDictionary<string, bool> ActivityByModule,
        IReadOnlyDictionary<string, bool> DataByModule,
        IReadOnlyDictionary<string, AuditRoute> Routes)
    {
        public static readonly Snapshot Defaults = new(
            true, true, true, true, true,
            new Dictionary<string, bool>(),
            new Dictionary<string, bool>(),
            new Dictionary<string, AuditRoute>());
    }
}
