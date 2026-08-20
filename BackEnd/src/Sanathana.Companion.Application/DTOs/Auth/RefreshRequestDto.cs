namespace Sanathana.Companion.Application.DTOs.Auth;

/// <summary>
/// Carries the refresh token in the body rather than a header or a cookie: a header would be
/// logged by proxies that log headers, and a cookie would need CSRF handling the app does not have.
/// </summary>
public class RefreshRequestDto
{
    public string RefreshToken { get; set; } = string.Empty;
}
