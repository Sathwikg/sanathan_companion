namespace App.Core.Common;

/// <summary>
/// The client's copy of the server's password rule, so a seeker learns the length before the
/// round trip rather than after it.
/// </summary>
/// <remarks>
/// The server still decides — see Sanathana.Companion.Application.Common.PasswordPolicy, which
/// also rejects obvious passwords and measures the 72-byte BCrypt ceiling in bytes. This side
/// only mirrors the two numbers a form needs to show.
/// </remarks>
public static class PasswordPolicy
{
    public const int MinimumLength = 10;
    public const int MaximumLength = 72;
}
