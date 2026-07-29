using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sanathana.Companion.Application.DTOs.Users;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Api.Controllers;

/// <summary>The signed-in user's own profile — available to any authenticated user (unlike the admin User master).</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
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
}
