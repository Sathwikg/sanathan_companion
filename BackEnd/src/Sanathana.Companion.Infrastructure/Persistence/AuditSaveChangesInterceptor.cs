using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Infrastructure.Persistence;

/// <summary>
/// Turns every insert, update and delete made while serving a request into an
/// <see cref="AuditDataLog"/> row.
/// </summary>
/// <remarks>
/// <para>
/// Captured before the save, because afterwards the original values are gone and deleted rows are
/// detached; published only after the save commits, because a change that rolled back did not
/// happen and must not be in the trail. A failed or cancelled save discards what was captured.
/// </para>
/// <para>
/// Only changes made inside an HTTP request are recorded. Start-up seeding and the background jobs
/// are the system talking to itself, and the localization seed alone would bury the log in
/// thousands of rows on a fresh database.
/// </para>
/// <para>
/// Changes to the audit configuration itself are always recorded, whatever the switches say, so
/// that pausing the audit, changing data and resuming it cannot pass unnoticed.
/// </para>
/// </remarks>
public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    /// <summary>Longest text value kept in the before/after JSON. Rich-text bodies can be enormous.</summary>
    public const int MaxValueLength = 2000;

    private static readonly HashSet<Type> NeverAudited = new()
    {
        typeof(AuditUserSession),
        typeof(AuditActivityLog),
        typeof(AuditDataLog),
        typeof(ErrorLog),
        // Rotated on every refresh; the session log already tells that story.
        typeof(RefreshToken)
    };

    private static readonly HashSet<Type> AlwaysAudited = new()
    {
        typeof(AuditSettings),
        typeof(AuditModuleConfig)
    };

    /// <summary>The row's own stamps. They change on every update and duplicate the log's own who/when.</summary>
    private static readonly HashSet<string> StampColumns = new(StringComparer.Ordinal)
    {
        "CreatedBy", "CreatedDate", "ModifiedBy", "ModifiedDate"
    };

    private static readonly string[] SecretMarkers = { "Password", "Hash", "Secret", "Token" };

    private static readonly JsonSerializerOptions Json = new()
    {
        // Telugu, Hindi, Tamil and Kannada text stays readable instead of becoming త escapes.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IAuditQueue _queue;
    private readonly IAuditConfigCache _cache;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditRequestContext _request;

    private List<Pending>? _pending;

    public AuditSaveChangesInterceptor(
        IAuditQueue queue,
        IAuditConfigCache cache,
        ICurrentUserService currentUser,
        IAuditRequestContext request)
    {
        _queue = queue;
        _cache = cache;
        _currentUser = currentUser;
        _request = request;
    }

    // ------------------------------------------------------------------ before the save

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        _pending = Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        _pending = Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    // ------------------------------------------------------------------ after it

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Publish();
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Publish();
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        _pending = null;
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _pending = null;
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    public override void SaveChangesCanceled(DbContextEventData eventData)
    {
        _pending = null;
        base.SaveChangesCanceled(eventData);
    }

    public override Task SaveChangesCanceledAsync(DbContextEventData eventData, CancellationToken cancellationToken = default)
    {
        _pending = null;
        return base.SaveChangesCanceledAsync(eventData, cancellationToken);
    }

    // ------------------------------------------------------------------ the work

    private List<Pending>? Capture(DbContext? context)
    {
        if (context is null || !_request.IsHttpRequest) return null;

        List<Pending>? pending = null;
        var now = DateTime.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;

            var clrType = entry.Metadata.ClrType;
            if (NeverAudited.Contains(clrType)) continue;

            var always = AlwaysAudited.Contains(clrType);
            var moduleCode = always ? ModuleCodes.AuditConfig : _request.ModuleCode;
            if (!always && !_cache.IsDataAuditEnabled(moduleCode)) continue;

            var log = Describe(entry, now, moduleCode);
            if (log is null) continue;

            (pending ??= new()).Add(new Pending(entry, log, entry.State == EntityState.Added));
        }

        return pending;
    }

    private AuditDataLog? Describe(EntityEntry entry, DateTime now, string? moduleCode)
    {
        var oldValues = new Dictionary<string, object?>();
        var newValues = new Dictionary<string, object?>();
        var changed = new List<string>();

        foreach (var prop in entry.Properties)
        {
            var name = prop.Metadata.Name;
            if (StampColumns.Contains(name)) continue;

            var secret = IsSecret(name);
            switch (entry.State)
            {
                case EntityState.Added:
                    newValues[name] = Value(prop.CurrentValue, secret);
                    changed.Add(name);
                    break;

                case EntityState.Deleted:
                    oldValues[name] = Value(prop.OriginalValue, secret);
                    changed.Add(name);
                    break;

                case EntityState.Modified when prop.IsModified && !Equals(prop.OriginalValue, prop.CurrentValue):
                    oldValues[name] = Value(prop.OriginalValue, secret);
                    newValues[name] = Value(prop.CurrentValue, secret);
                    changed.Add(name);
                    break;
            }
        }

        // An update that only touched the stamps changed nothing anyone can see.
        if (changed.Count == 0) return null;

        return new AuditDataLog
        {
            UserId = _currentUser.UserId,
            UsernameOrEmail = _currentUser.Email,
            Action = entry.State switch
            {
                EntityState.Added => "INSERT",
                EntityState.Deleted => "DELETE",
                _ => "UPDATE"
            },
            EntityName = entry.Metadata.HasSharedClrType ? entry.Metadata.Name : entry.Metadata.ClrType.Name,
            EntityId = KeyOf(entry),
            ModuleCode = moduleCode,
            ChangedColumns = string.Join(", ", changed),
            OldValuesJson = Serialize(oldValues),
            NewValuesJson = Serialize(newValues),
            TimestampUtc = now,
            IpAddress = _request.IpAddress,
            Endpoint = _request.Endpoint
        };
    }

    private void Publish()
    {
        var pending = _pending;
        _pending = null;
        if (pending is null) return;

        foreach (var item in pending)
        {
            // A database-generated key only exists once the insert has run.
            if (item.IsInsert) item.Log.EntityId = KeyOf(item.Entry);
            _queue.EnqueueDataLog(item.Log);
        }
    }

    private static string? KeyOf(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key is null) return null;

        var parts = key.Properties.Select(p => entry.Property(p.Name).CurrentValue?.ToString());
        return string.Join(",", parts);
    }

    private static bool IsSecret(string propertyName)
        => SecretMarkers.Any(m => propertyName.Contains(m, StringComparison.OrdinalIgnoreCase));

    /// <summary>What goes into the JSON for one value: binaries and secrets are described, not copied.</summary>
    private static object? Value(object? value, bool secret)
    {
        if (value is null) return null;
        if (secret) return "[redacted]";

        return value switch
        {
            // Deity images, wallpapers and chant audio are megabytes each; the fact of the change is
            // what the trail needs, not a base64 copy of the file.
            byte[] bytes => $"[binary {bytes.Length.ToString("N0", CultureInfo.InvariantCulture)} bytes]",
            string s when s.Length > MaxValueLength => s[..MaxValueLength] + "…",
            _ => value
        };
    }

    private static string? Serialize(Dictionary<string, object?> values)
    {
        if (values.Count == 0) return null;

        try
        {
            return JsonSerializer.Serialize(values, Json);
        }
        catch (Exception)
        {
            // A value type the serializer cannot handle must not cost the record; text will do.
            var asText = values.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString());
            return JsonSerializer.Serialize(asText, Json);
        }
    }

    private sealed record Pending(EntityEntry Entry, AuditDataLog Log, bool IsInsert);
}
