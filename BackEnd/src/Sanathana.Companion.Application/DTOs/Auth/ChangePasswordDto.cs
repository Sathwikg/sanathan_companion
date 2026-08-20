namespace Sanathana.Companion.Application.DTOs.Auth;

/// <summary>Changes the signed-in user's own password.</summary>
public class ChangePasswordDto
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
