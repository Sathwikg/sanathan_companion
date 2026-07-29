namespace Sanathana.Companion.Infrastructure.Persistence;

/// <summary>
/// Builds a safe LIKE/ILIKE "contains" pattern from user input. The term is already
/// parameterised by EF (so this is not about SQL injection) — this escapes the LIKE
/// metacharacters %, _ and \ so a user searching a literal "%" matches "%", not everything.
/// Use with the ESCAPE character <see cref="EscapeChar"/>.
/// </summary>
internal static class SqlLike
{
    public const string EscapeChar = "\\";

    public static string Contains(string term)
        => "%" + term.Trim()
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_") + "%";
}
