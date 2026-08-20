namespace Sanathana.Companion.Domain.Common;

/// <summary>
/// Turns a typed-in email address or mobile number into the single spelling the database stores.
/// </summary>
/// <remarks>
/// It lives in Domain because both the repository interface and its implementation have to agree
/// on it: a unique index is only as good as the normalisation applied before every write, and a
/// helper only one side can see is a convention rather than a contract.
/// </remarks>
public static class CredentialNormalizer
{
    /// <summary>Trimmed and lower-cased. No mail provider distinguishes case, so two spellings must be one account.</summary>
    public static string Email(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>
    /// Digits only. An Indian country code or trunk prefix is dropped when what is left is a
    /// ten-digit number, so +91 98765 43210, 098765 43210 and 9876543210 are one seeker.
    /// </summary>
    /// <returns>The digits, or null when the value carries none.</returns>
    public static string? Mobile(string? value)
    {
        var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());

        if (digits.Length == 12 && digits.StartsWith("91", StringComparison.Ordinal)) digits = digits[2..];
        else if (digits.Length == 11 && digits[0] == '0') digits = digits[1..];

        return digits.Length == 0 ? null : digits;
    }
}
