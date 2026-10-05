namespace Sanathana.Companion.Application.Interfaces;

/// <summary>
/// The audit switches, held in memory so the hot paths (every SaveChanges, every page visit) never
/// query for them.
/// </summary>
/// <remarks>
/// Loaded when the API starts, reloaded after the configuration screen saves, and reloaded again
/// every minute so a second API instance cannot drift from what an administrator set.
/// </remarks>
public interface IAuditConfigCache
{
    bool IsGlobalAuditEnabled { get; }
    bool TrackUserSessions { get; }
    bool TrackPageNavigation { get; }
    bool TrackDataModifications { get; }

    /// <summary>Independent of the master switch: diagnostics keep flowing while business auditing is paused.</summary>
    bool TrackErrorLogs { get; }

    /// <summary>Page-visit auditing for one form. A form with no saved rule follows the global switch.</summary>
    bool IsActivityAuditEnabled(string? moduleCode);

    /// <summary>Data-change auditing for one form. A change made outside any form follows the global switch.</summary>
    bool IsDataAuditEnabled(string? moduleCode);

    /// <summary>The form a client route belongs to, from the Modules table, or null for a page that is not a form.</summary>
    AuditRoute? ResolveRoute(string? routePath);

    Task EnsureInitializedAsync(CancellationToken ct = default);
    Task ReloadAsync(CancellationToken ct = default);
}

/// <summary>A form as the audit log names it: the stable code, and the display name at the time.</summary>
public sealed record AuditRoute(string ModuleCode, string FormName);
