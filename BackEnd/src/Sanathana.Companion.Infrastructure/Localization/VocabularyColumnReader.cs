using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Infrastructure.Persistence;

namespace Sanathana.Companion.Infrastructure.Localization;

/// <summary>
/// Reads distinct values from a registered column.
/// </summary>
/// <remarks>
/// SECURITY: table and column names arrive from an admin-editable table, so they are untrusted
/// input to a query that cannot use parameters for identifiers. Rather than escaping them, this
/// resolves them through the EF model and emits ONLY the names EF itself reports. A name that is
/// not part of the model yields no query at all, so an injected identifier can never reach SQL.
/// </remarks>
public class VocabularyColumnReader : IVocabularyColumnReader
{
    private readonly ApplicationDbContext _context;

    public VocabularyColumnReader(ApplicationDbContext context) => _context = context;

    public async Task<IReadOnlyList<string>> ReadDistinctAsync(
        string tableName, string columnName, int max, CancellationToken cancellationToken = default)
    {
        var entityType = _context.Model.GetEntityTypes()
            .FirstOrDefault(e => string.Equals(e.GetTableName(), tableName, StringComparison.Ordinal));
        if (entityType is null) return [];

        var property = entityType.GetProperties()
            .FirstOrDefault(p => string.Equals(p.GetColumnName(), columnName, StringComparison.Ordinal));
        if (property is null || property.ClrType != typeof(string)) return [];

        // Both identifiers now come from the model, never from the caller's strings.
        var table = entityType.GetTableName()!;
        var column = property.GetColumnName();
        var schema = entityType.GetSchema() ?? "public";
        var limit = Math.Clamp(max, 1, 20_000);

        var sql = $"""
            SELECT DISTINCT "{column}" AS "Value"
            FROM "{schema}"."{table}"
            WHERE "{column}" IS NOT NULL AND "{column}" <> ''
            LIMIT {limit}
            """;

        return await _context.Database
            .SqlQueryRaw<string>(sql)
            .ToListAsync(cancellationToken);
    }
}
