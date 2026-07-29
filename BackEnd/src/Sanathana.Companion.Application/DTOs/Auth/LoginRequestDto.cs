namespace Sanathana.Companion.Application.DTOs.Auth;

public class LoginRequestDto
{
    /// <summary>Email address or mobile number.</summary>
    public string Credential { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
