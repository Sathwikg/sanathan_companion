using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.Common.Authorization;
using Sanathana.Companion.Application.DTOs.Users;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Api.Controllers;

/// <summary>The signed-in user's own profile — available to any authenticated user (unlike the admin User master).</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[ModuleExempt]
public class ProfileController : ControllerBase
{
    private readonly IUserService _users;
    private readonly ICurrentUserService _currentUser;

    public ProfileController(IUserService users, ICurrentUserService currentUser)
    {
        _users = users;
        _currentUser = currentUser;
    }

    /// <summary>The current user's profile and per-day sadhana timeline.</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(MyProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (userId is null) return Unauthorized();

        var dto = await _users.GetMyProfileAsync(userId.Value, cancellationToken);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Set (or clear) the current user's preferred region.</summary>
    [HttpPut("region")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SetDefaultRegion([FromBody] UpdateDefaultRegionDto dto, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (userId is null) return Unauthorized();

        await _users.UpdateDefaultRegionAsync(userId.Value, dto.RegionId, cancellationToken);
        return NoContent();
    }

    /// <summary>Everything the app holds about the caller, as a JSON download.</summary>
    /// <remarks>
    /// Together with DELETE below this is what both stores now require of any app that lets people
    /// create an account, and what a seeker is entitled to ask for regardless.
    /// </remarks>
    [HttpGet("export")]
    [ProducesResponseType(typeof(MyDataExportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ExportMyData(CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (userId is null) return Unauthorized();

        var export = await _users.ExportMyDataAsync(userId.Value, cancellationToken);
        if (export is null) return NotFound();

        // Content-Disposition so a browser or WebView saves it rather than rendering it. The
        // filename is fixed — deriving it from the user's name would put personal data into a
        // header, and into whatever logs that header.
        return File(
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(export, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            }),
            "application/json",
            "sanathana-companion-my-data.json");
    }

    /// <summary>Permanently deletes the caller's own account and everything belonging to them.</summary>
    [HttpDelete("me")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteMyAccount([FromBody] DeleteAccountDto dto, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (userId is null) return Unauthorized();

        var deleted = await _users.DeleteMyAccountAsync(userId.Value, dto.Password, cancellationToken);

        // 400, not 401: the session is valid and the caller stays signed in — it is the password
        // they typed to confirm that did not match.
        return deleted
            ? NoContent()
            : BadRequest(new { message = "That password is incorrect, so the account was not deleted." });
    }
}
