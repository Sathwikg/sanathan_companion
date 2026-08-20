using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sanathana.Companion.Application.DTOs.Auth;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ICurrentUserService _currentUser;

    public AuthController(IAuthService authService, ICurrentUserService currentUser)
    {
        _authService = authService;
        _currentUser = currentUser;
    }

    /// <summary>Registers a new seeker (auto-assigned the Sanathan role).</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request, CancellationToken cancellationToken)
    {
        var message = await _authService.RegisterAsync(request, cancellationToken);
        return Ok(new { message });
    }

    /// <summary>Authenticates with email-or-mobile + password and returns a JWT.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);
        return result is null
            ? Unauthorized(new { message = "Invalid credentials." })
            : Ok(result);
    }

    /// <summary>Exchanges a refresh token for a fresh pair.</summary>
    /// <remarks>
    /// Anonymous because possession of the refresh token IS the credential — requiring a live
    /// access token would make this endpoint useless at exactly the moment it is needed. Its own
    /// rate-limit policy, because the class-level "auth" bucket is sized for sign-in attempts and
    /// a phone waking up refreshes far more often than a person types a password.
    /// </remarks>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("refresh")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _authService.RefreshAsync(request, cancellationToken);
        return result is null
            ? Unauthorized(new { message = "Please sign in again." })
            : Ok(result);
    }

    /// <summary>Revokes the refresh-token family the given token belongs to.</summary>
    /// <remarks>
    /// Always 204, whether or not the token existed: answering differently would turn this into an
    /// oracle for whether a stolen token is still live. Anonymous for the same reason as refresh —
    /// a seeker whose access token just died must still be able to end the session.
    /// </remarks>
    [HttpPost("logout")]
    [AllowAnonymous]
    [EnableRateLimiting("refresh")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] RefreshRequestDto request, CancellationToken cancellationToken)
    {
        await _authService.LogoutAsync(request, cancellationToken);
        return NoContent();
    }

    /// <summary>Changes the signed-in user's own password and returns a fresh token pair.</summary>
    /// <remarks>
    /// Until this existed there was no way to rotate ANY password through the API — including the
    /// seeded administrator's, which is why that account had to ship locked.
    /// <para>
    /// The new pair matters: changing a password revokes every token the account holds, so without
    /// it the seeker would be signed out by their own precaution. Every OTHER device is signed out,
    /// which is the point.
    /// </para>
    /// </remarks>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (userId is null) return Unauthorized();

        var result = await _authService.ChangePasswordAsync(userId.Value, request, cancellationToken);

        // 400, not 401: the session is perfectly valid — it is the supplied current password that
        // is wrong, and a 401 here would sign the user out of a session that never expired.
        return result is null
            ? BadRequest(new { message = "Your current password is incorrect." })
            : Ok(result);
    }
}
