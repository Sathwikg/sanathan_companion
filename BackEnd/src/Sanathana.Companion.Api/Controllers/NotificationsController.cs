using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sanathana.Companion.Application.DTOs.Notifications;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Api.Controllers;

/// <summary>Admin: decides which modules may raise notifications.</summary>
[ApiController]
[Route("api/notificationconfig")]
[Authorize(Roles = "Admin")]
public class NotificationConfigController : ControllerBase
{
    private readonly INotificationService _service;

    public NotificationConfigController(INotificationService service) => _service = service;

    /// <summary>Every navigable form with its notification configuration.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(NotificationConfigListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
        => Ok(await _service.GetConfigAsync(cancellationToken));

    /// <summary>Saves which modules may notify, and how they default for users.</summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Save([FromBody] SaveNotificationConfigDto dto, CancellationToken cancellationToken)
    {
        await _service.SaveConfigAsync(dto, cancellationToken);
        return NoContent();
    }
}

/// <summary>The signed-in user's own notification preferences.</summary>
[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _service;
    private readonly ICurrentUserService _currentUser;

    public NotificationsController(INotificationService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    /// <summary>What I get notified about, and whether each would fire right now.</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(MyNotificationSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (userId is null) return Unauthorized();
        return Ok(await _service.GetMySettingsAsync(userId.Value, cancellationToken));
    }

    [HttpPut("me")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SaveMine([FromBody] SaveMyNotificationSettingsDto dto, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (userId is null) return Unauthorized();

        await _service.SaveMySettingsAsync(userId.Value, dto, cancellationToken);
        return NoContent();
    }
}
