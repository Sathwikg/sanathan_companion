namespace Sanathana.Companion.Application.DTOs.Auth;

public class RegisterRequestDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string? SeekerName { get; set; }

    /// <summary>Optional preferred region — becomes the seeker's default region.</summary>
    public Guid? RegionId { get; set; }
}
