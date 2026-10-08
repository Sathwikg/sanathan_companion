using Microsoft.EntityFrameworkCore;

namespace Sanathana.Companion.Infrastructure.Audit;

/// <summary>
/// Trims every string on an audit row to its column's declared length and strips NUL characters.
/// </summary>
/// <remarks>
/// Driven by the EF model rather than a hand-kept list, so a column resized in the configuration is
/// honoured here automatically. NUL is removed because PostgreSQL rejects it in text outright, and
/// client-supplied stack traces are exactly where one turns up.
/// </remarks>
public static class AuditColumnFitter
{
    public static void Fit(DbContext db, object entity)
    {
        var entityType = db.Model.FindEntityType(entity.GetType());
        if (entityType is null) return;

        foreach (var property in entityType.GetProperties())
        {
            if (property.ClrType != typeof(string) || property.PropertyInfo is not { } info) continue;
            if (info.GetValue(entity) is not string value) continue;

            var fitted = value.Contains('\0') ? value.Replace("\0", string.Empty) : value;
            if (property.GetMaxLength() is int max && fitted.Length > max)
            {
                // Do not cut an emoji or a supplementary character in half: Npgsql's UTF-8
                // encoder rejects a lone surrogate, which would fail the whole row.
                var cut = char.IsHighSurrogate(fitted[max - 1]) ? max - 1 : max;
                fitted = fitted[..cut];
            }

            if (!ReferenceEquals(fitted, value))
                info.SetValue(entity, fitted);
        }
    }
}
