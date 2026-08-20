using Npgsql;

namespace Sanathana.Companion.Api.Configuration;

/// <summary>
/// Answers whether the configured database connection actually checks who it is talking to.
/// </summary>
/// <remarks>
/// Two traps here, and a substring search for the obvious keyword falls into both.
/// <para>
/// <c>SSL Mode=Require</c> reads like the safe option and is not: in Npgsql it means "refuse to
/// connect without TLS" and nothing more — no chain check, no hostname check. Only VerifyCA and
/// VerifyFull validate, so a plain <c>SSL Mode=Require</c> string is exactly as open to anyone
/// sitting between here and the database as one that says Trust Server Certificate.
/// </para>
/// <para>
/// And <c>Trust Server Certificate</c> is inert in Npgsql 10 — the property is marked obsolete
/// with "no longer needed and does nothing". Looking for it would therefore flag strings that are
/// fine and miss the ones that are not. SslMode is the only thing worth reading.
/// </para>
/// </remarks>
public static class ConnectionStringGuard
{
    public static bool SkipsCertificateValidation(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return false;

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            return builder.SslMode is not (SslMode.VerifyCA or SslMode.VerifyFull);
        }
        catch (ArgumentException)
        {
            // A string this malformed is the DbContext's problem to report, with a far better
            // message than anything a start-up warning could manage.
            return false;
        }
    }
}
