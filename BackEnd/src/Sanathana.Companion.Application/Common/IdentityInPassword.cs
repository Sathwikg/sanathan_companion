namespace Sanathana.Companion.Application.Common;

/// <summary>
/// A password built out of the address or number typed two fields above is the first thing anyone
/// guesses. Shared by the plain and the Google registration paths, which learn the email in
/// different places (the form and the ticket, respectively) but apply the same rule.
/// </summary>
public static class IdentityInPassword
{
    /// <summary>
    /// True when the password contains the email's local part or the mobile's digits. The length
    /// floors matter: without them an address like m@example.com would reject every password
    /// containing the letter m.
    /// </summary>
    public static bool Contains(string? email, string? mobileDigits, string? password)
    {
        if (string.IsNullOrEmpty(password)) return false;

        var local = (email ?? string.Empty).Split('@')[0];
        if (local.Length >= 4 && password.Contains(local, StringComparison.OrdinalIgnoreCase)) return true;

        return mobileDigits is { Length: >= 6 } && password.Contains(mobileDigits, StringComparison.Ordinal);
    }
}
