using System.Text;
using FluentValidation;

namespace Sanathana.Companion.Application.Common;

/// <summary>
/// The one place that decides what counts as an acceptable password.
/// </summary>
/// <remarks>
/// Registration, the change-password form and the administrator bootstrapper each used to carry
/// their own rule — 6, 8 and 8 characters — which meant the weakest of the three was the real
/// policy. Length does the work here rather than a character-class rule: a memorable phrase beats
/// P@ss1! and a seeker typing Telugu or Tamil should not be forced through an English keyboard.
/// </remarks>
public static class PasswordPolicy
{
    public const int MinimumLength = 10;

    /// <summary>BCrypt reads no further than 72 <em>bytes</em>, so anything longer is silently truncated.</summary>
    public const int MaximumByteLength = 72;

    private static readonly HashSet<string> TooObvious = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "password1", "password123", "1234567890", "qwertyuiop",
        "iloveyou", "letmein", "sanathana", "sanathan1", "abcdefghij"
    };

    public static bool IsAcceptable(string? password)
    {
        // Null-safe on purpose: a JSON body may carry an explicit null, and FluentValidation's
        // default cascade keeps running later rules after NotEmpty has already failed.
        if (string.IsNullOrEmpty(password)) return false;
        if (password.Length < MinimumLength) return false;
        if (Encoding.UTF8.GetByteCount(password) > MaximumByteLength) return false;
        if (password.Distinct().Count() == 1) return false;

        return !TooObvious.Contains(password);
    }

    public static IRuleBuilderOptions<T, string> MustSatisfyPolicy<T>(this IRuleBuilder<T, string> rule)
        => rule.Must(IsAcceptable)
               .WithMessage($"Password must be at least {MinimumLength} characters, and not an obvious one.");
}
