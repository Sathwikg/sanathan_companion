namespace Sanathana.Companion.Application.DTOs.Auth;

/// <summary>
/// The fields every way of creating an account collects, so the plain and the Google registration
/// validators share one set of rules instead of drifting apart.
/// </summary>
public interface IRegistrationFields
{
    string FullName { get; }
    string MobileNumber { get; }
    string Password { get; }
    string ConfirmPassword { get; }
    string? SeekerName { get; }
}
