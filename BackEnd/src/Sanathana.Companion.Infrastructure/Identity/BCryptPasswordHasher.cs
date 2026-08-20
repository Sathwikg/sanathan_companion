using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Infrastructure.Identity;

public class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 11;

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    /// <summary>Checks a password against a stored hash. A hash it cannot parse is a failed check, not an error.</summary>
    /// <remarks>
    /// BCrypt.Net throws rather than returning false on a hash it cannot read, and it throws three
    /// different types depending on how the value is malformed: <c>ArgumentException</c> for empty,
    /// <c>SaltParseException</c> for unparsable, and <c>ArgumentOutOfRangeException</c> for a
    /// truncated hash. All three are pinned by SeededAdminTests against the referenced version. Unguarded, one unreadable row turns
    /// every sign-in attempt for that account into a 500 instead of a clean 401 — and a 500 where
    /// other accounts get a 401 tells an attacker the account exists.
    /// </remarks>
    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrWhiteSpace(hash)) return false;

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (Exception ex) when (ex is BCrypt.Net.SaltParseException
                                      or ArgumentException
                                      or ArgumentOutOfRangeException)
        {
            // An unreadable hash cannot match any password. Fail closed and quietly.
            return false;
        }
    }
}
