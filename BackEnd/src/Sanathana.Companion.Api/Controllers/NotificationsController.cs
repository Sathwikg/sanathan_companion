using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.Common.Authorization;
using Sanathana.Companion.Application.DTOs.Notifications;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Api.Controllers;

/// <summary>Admin: decides which modules may raise notifications.</summary>
[ApiController]
[Route("api/notificationconfig")]
[Authorize(Roles = "Admin")]
[RequiresModule(ModuleCodes.NotificationConfig)]
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

/// <summary>The signed-in user's own notification preferences, their phone's registration and their inbox.</summary>
[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _service;
    private readonly INotificationStore _store;
    private readonly ICurrentUserService _currentUser;

    public NotificationsController(INotificationService service, INotificationStore store, ICurrentUserService currentUser)
    {
        _service = service;
        _store = store;
        _currentUser = currentUser;
    }

    /// <summary>What I get notified about, and whether each would fire right now.</summary>
    /// <remarks>
    /// Exempt: this fills the bell in the mobile top bar for every signed-in seeker, and the client
    /// treats it as best-effort. Gating it on the My Notifications form would empty the badge for
    /// anyone not granted that screen.
    /// </remarks>
    [ModuleExempt]
    [HttpGet("me")]
    [ProducesResponseType(typeof(MyNotificationSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (userId is null) return Unauthorized();
        return Ok(await _service.GetMySettingsAsync(userId.Value, cancellationToken));
    }

    /// <summary>Only the My Notifications page writes these.</summary>
    [RequiresModule(ModuleCodes.MyNotifications)]
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

    // ---------------- this phone ----------------

    /// <summary>The phone introducing itself after sign-in, so notifications can reach it.</summary>
    /// <remarks>
    /// Exempt for the same reason as <see cref="GetMine"/>: every signed-in seeker's phone does this
    /// at start-up, whether or not their role was granted the My Notifications screen. Called again
    /// whenever the token, the language or the time zone changes; the same token simply updates.
    /// </remarks>
    [ModuleExempt]
    [HttpPost("devices")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceDto dto, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (userId is null) return Unauthorized();

        var token = dto.Token?.Trim();
        if (string.IsNullOrEmpty(token) || token.Length > 4096) return BadRequest("A device token is required.");
        if (dto.Platform is not ((int)PushPlatform.Android or (int)PushPlatform.iOS)) return BadRequest("Unknown platform.");

        await _store.UpsertDeviceAsync(userId.Value, dto.Platform, token, dto.DeviceModel, dto.AppVersion, dto.TimeZone, dto.Language, cancellationToken);
        return NoContent();
    }

    /// <summary>The phone taking itself off the list at sign-out.</summary>
    /// <remarks>
    /// Anonymous on purpose. Sign-out is also what happens when a session has expired, and by then
    /// there is no valid access token to send; refusing the call would leave the previous seeker's
    /// notifications arriving on a phone someone else may now be using. The device token is itself
    /// the credential: it is long, random and known only to that installation, and all it can do
    /// here is stop notifications to itself.
    /// </remarks>
    [AllowAnonymous]
    [HttpPost("devices/remove")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveDevice([FromBody] RemoveDeviceDto dto, CancellationToken cancellationToken)
    {
        var token = dto.Token?.Trim();
        if (!string.IsNullOrEmpty(token) && token.Length <= 4096)
            await _store.RemoveDeviceAsync(token, cancellationToken);

        // The same answer whether or not the token was known: nothing to learn by guessing.
        return NoContent();
    }

    // ---------------- inbox ----------------

    /// <summary>The notifications this seeker was sent, newest first, with how many are unread.</summary>
    [ModuleExempt]
    [HttpGet("inbox")]
    [ProducesResponseType(typeof(InboxDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Inbox([FromQuery] int take = 30, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        if (userId is null) return Unauthorized();

        var items = await _store.GetInboxAsync(userId.Value, take, cancellationToken);
        return Ok(new InboxDto
        {
            Unread = await _store.CountUnreadAsync(userId.Value, cancellationToken),
            Items = items.Select(l => new InboxItemDto
            {
                Id = l.Id,
                Kind = l.Kind,
                Title = l.Title,
                Body = l.Body,
                Route = l.Route,
                SentAtUtc = l.SentAtUtc ?? l.CreatedDate,
                IsRead = l.ReadAtUtc is not null
            }).ToList()
        });
    }

    /// <summary>Marks everything in the inbox as read; the bell's badge clears.</summary>
    [ModuleExempt]
    [HttpPost("inbox/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkInboxRead(CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (userId is null) return Unauthorized();

        await _store.MarkInboxReadAsync(userId.Value, DateTime.UtcNow, cancellationToken);
        return NoContent();
    }
}
