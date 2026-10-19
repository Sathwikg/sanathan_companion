using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.Common.Authorization;
using Sanathana.Companion.Application.DTOs.Notifications;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Api.Controllers;

/// <summary>
/// Admin endpoints for managing push notification configuration and sending notifications.
/// </summary>
[ApiController]
[Route("api/push-notifications")]
[Authorize(Roles = "Admin")]
[RequiresModule(ModuleCodes.PushNotifications)]
public sealed class PushNotificationController : ControllerBase
{
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IOptions<PushNotificationOptions> _options;

    public PushNotificationController(IPushNotificationService pushNotificationService, IOptions<PushNotificationOptions> options)
    {
        _pushNotificationService = pushNotificationService;
        _options = options;
    }

    // ========== Configuration ==========

    /// <summary>
    /// The effective push settings (appsettings "PushNotifications" section plus environment), for the
    /// admin screen to show. Read-only: the credential files are reported as configured or not, never by
    /// path or content.
    /// </summary>
    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        var o = _options.Value;
        return Ok(new
        {
            enabled = o.Enabled,
            fcmConfigured = o.IsFcmConfigured,
            fcmProjectId = o.FcmProjectId,
            fcmSenderId = o.FcmSenderId,
            apnsConfigured = o.IsApnsConfigured,
            apnsKeyId = o.ApnsKeyId,
            apnsTeamId = o.ApnsTeamId,
            apnsBundleId = o.ApnsBundleId,
            apnsUseSandbox = o.ApnsUseSandbox,
            defaultTitle = o.DefaultTitle,
            defaultIcon = o.DefaultIcon,
            defaultColor = o.DefaultColor,
            timeToLiveSeconds = o.TimeToLiveSeconds,
            maxRetryAttempts = o.MaxRetryAttempts,
            batchSize = o.BatchSize
        });
    }

    /// <summary>
    /// Gets push notification statistics.
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken = default)
    {
        var stats = await _pushNotificationService.GetStatsAsync(cancellationToken);
        return Ok(stats);
    }

    /// <summary>
    /// Cleans up inactive device tokens.
    /// </summary>
    [HttpPost("cleanup-tokens")]
    public async Task<IActionResult> CleanupTokens([FromQuery] int olderThanDays = 30, CancellationToken cancellationToken = default)
    {
        var deleted = await _pushNotificationService.CleanupInactiveTokensAsync(olderThanDays, cancellationToken);
        return Ok(new { deleted, message = $"Cleaned up {deleted} inactive tokens" });
    }

    // ========== Device Token Management ==========

    /// <summary>
    /// Gets all device tokens (admin view).
    /// </summary>
    [HttpGet("device-tokens")]
    public async Task<IActionResult> GetDeviceTokens(CancellationToken cancellationToken = default)
    {
        // This would need a new method in the service - for now return stats
        var stats = await _pushNotificationService.GetStatsAsync(cancellationToken);
        return Ok(stats);
    }

    /// <summary>
    /// Removes a specific device token.
    /// </summary>
    [HttpDelete("device-tokens/{token}")]
    public async Task<IActionResult> RemoveDeviceToken(string token, CancellationToken cancellationToken = default)
    {
        // Note: In a real implementation, you'd need to get the userId from the token
        // This is a placeholder
        await _pushNotificationService.MarkTokenInactiveAsync(token, "Admin removed", cancellationToken);
        return Ok(new { message = "Token marked inactive" });
    }

    // ========== Sending Notifications ==========

    /// <summary>
    /// Sends a push notification to specific users.
    /// </summary>
    [HttpPost("send-to-users")]
    public async Task<IActionResult> SendToUsers([FromBody] SendPushNotificationRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.UserIds.Any())
            return BadRequest("At least one user ID is required");

        if (string.IsNullOrWhiteSpace(request.Title) && string.IsNullOrWhiteSpace(request.Body))
            return BadRequest("Title or body is required");

        var result = await _pushNotificationService.SendToUsersAsync(request, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Sends a push notification to all subscribers of a module.
    /// </summary>
    [HttpPost("send-to-module")]
    public async Task<IActionResult> SendToModule([FromBody] SendModuleNotificationRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title) && string.IsNullOrWhiteSpace(request.Body))
            return BadRequest("Title or body is required");

        var result = await _pushNotificationService.SendToModuleSubscribersAsync(request, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Sends a test notification to a specific user.
    /// </summary>
    [HttpPost("send-test")]
    public async Task<IActionResult> SendTest([FromBody] SendTestNotificationRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title) && string.IsNullOrWhiteSpace(request.Body))
            return BadRequest("Title or body is required");

        var result = await _pushNotificationService.SendTestNotificationAsync(request.UserId, request.Title, request.Body, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

/// <summary>
/// Request DTO for sending test notifications.
/// </summary>
public sealed record SendTestNotificationRequest(
    Guid UserId,
    string Title,
    string Body
);