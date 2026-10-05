using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Domain.Interfaces;

/// <summary>Reads for the audit screens. Writes of log rows go through the background queue instead.</summary>
public interface IAuditRepository
{
    /// <summary>The single settings row, tracked, or null when it has never been created.</summary>
    Task<AuditSettings?> GetSettingsAsync(CancellationToken ct = default);
    Task AddSettingsAsync(AuditSettings settings, CancellationToken ct = default);

    Task<List<AuditModuleConfig>> GetModuleConfigsAsync(CancellationToken ct = default);
    Task AddModuleConfigAsync(AuditModuleConfig config, CancellationToken ct = default);

    Task<AuditPage<AuditUserSession>> QuerySessionsAsync(AuditLogFilter filter, CancellationToken ct = default);
    Task<AuditPage<AuditActivityLog>> QueryActivitiesAsync(AuditLogFilter filter, CancellationToken ct = default);
    Task<AuditPage<AuditDataLog>> QueryDataLogsAsync(AuditLogFilter filter, CancellationToken ct = default);
    Task<AuditPage<ErrorLog>> QueryErrorsAsync(AuditLogFilter filter, CancellationToken ct = default);

    Task<ErrorLog?> GetErrorByIdAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// One page of a log, newest first. <see cref="Search"/> is matched case-insensitively against the
/// text columns that table offers; the date bounds are UTC, From inclusive and To exclusive.
/// </summary>
public sealed record AuditLogFilter(
    int Skip,
    int Take,
    string? Search = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    string? Action = null,
    string? Source = null,
    bool? Resolved = null);

public sealed record AuditPage<T>(List<T> Items, int TotalCount);
