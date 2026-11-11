using Hangfire;
using Hangfire.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.Common.Authorization;
using Sanathana.Companion.Application.DTOs.Notifications;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Application.Notifications;
using Sanathana.Companion.Application.Services.BackgroundJobs;

namespace Sanathana.Companion.Api.Controllers;

/// <summary>
/// The administrator's notification console: every notification type as a job with its schedule,
/// what it last did, the delivery history, the registered phones, and the buttons to run one now,
/// send a sample to oneself, pause it, or broadcast a message.
/// </summary>
[ApiController]
[Route("api/notification-console")]
[Authorize(Roles = "Admin")]
[RequiresModule(ModuleCodes.PushNotifications)]
public sealed class NotificationConsoleController : ControllerBase
{
    private readonly INotificationConsoleService _console;
    private readonly ICurrentUserService _currentUser;

    public NotificationConsoleController(INotificationConsoleService console, ICurrentUserService currentUser)
    {
        _console = console;
        _currentUser = currentUser;
    }

    /// <summary>Push readiness, the engine's heartbeat, today's figures and every notification type.</summary>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(ConsoleOverviewDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Overview(CancellationToken cancellationToken)
    {
        var overview = await _console.GetOverviewAsync(cancellationToken);
        overview.Engine = ReadHeartbeat();
        return Ok(overview);
    }

    /// <summary>
    /// The recurring job as Hangfire reports it. Read here rather than in the application layer
    /// because Hangfire's storage is a concern of the host; when background jobs are switched off
    /// there is no storage at all, which is reported as "not scheduled" rather than as an error.
    /// </summary>
    private static ConsoleEngineStatusDto ReadHeartbeat()
    {
        try
        {
            using var connection = JobStorage.Current.GetConnection();
            var job = connection.GetRecurringJobs().FirstOrDefault(j => j.Id == NotificationEngineJob.RecurringJobId);
            if (job is null) return new ConsoleEngineStatusDto { Scheduled = false };

            return new ConsoleEngineStatusDto
            {
                Scheduled = true,
                Cron = job.Cron,
                LastRunUtc = job.LastExecution,
                NextRunUtc = job.NextExecution,
                LastState = job.LastJobState
            };
        }
        catch (InvalidOperationException)
        {
            return new ConsoleEngineStatusDto { Scheduled = false };
        }
    }

    /// <summary>The delivery history, newest first.</summary>
    [HttpGet("logs")]
    [ProducesResponseType(typeof(ConsoleLogPageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Logs([FromQuery] string? kind, [FromQuery] string? status, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
        => Ok(await _console.GetLogsAsync(kind, status, search, page, pageSize, cancellationToken));

    /// <summary>Recent engine passes that had something to do, newest first.</summary>
    [HttpGet("runs")]
    [ProducesResponseType(typeof(IReadOnlyList<ConsoleRunDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Runs([FromQuery] string? kind, [FromQuery] int take = 50, CancellationToken cancellationToken = default)
        => Ok(await _console.GetRunsAsync(kind, take, cancellationToken));

    /// <summary>The registered phones, most recently used first. Tokens are never returned whole.</summary>
    [HttpGet("devices")]
    [ProducesResponseType(typeof(IReadOnlyList<ConsoleDeviceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Devices([FromQuery] int take = 200, CancellationToken cancellationToken = default)
        => Ok(await _console.GetDevicesAsync(take, cancellationToken));

    /// <summary>One ordinary pass over every type, right now.</summary>
    [HttpPost("run")]
    [ProducesResponseType(typeof(ConsoleActionResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> RunAll(CancellationToken cancellationToken)
        => Ok(await _console.RunNowAsync(null, cancellationToken));

    /// <summary>
    /// Runs one type now as if its time had come. Sends real notifications, to the seekers its own
    /// condition and their settings allow, minus anyone who already had this occasion's.
    /// </summary>
    [HttpPost("kinds/{kind}/run")]
    [ProducesResponseType(typeof(ConsoleActionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RunKind(string kind, CancellationToken cancellationToken)
        => Ok(await _console.RunNowAsync(kind, cancellationToken));

    /// <summary>Sends a sample of one type to one seeker (the caller by default), whatever their settings say.</summary>
    [HttpPost("kinds/{kind}/test")]
    [ProducesResponseType(typeof(ConsoleActionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TestKind(string kind, [FromBody] ConsoleTestRequestDto? request, CancellationToken cancellationToken)
    {
        var userId = request?.UserId ?? _currentUser.UserId;
        if (userId is null) return Unauthorized();

        return Ok(await _console.SendTestAsync(kind, userId.Value, cancellationToken));
    }

    /// <summary>Pauses or resumes one type for everyone. Seekers' own choices are kept.</summary>
    [HttpPut("kinds/{kind}/enabled")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetEnabled(string kind, [FromBody] ConsoleEnabledRequestDto request, CancellationToken cancellationToken)
    {
        await _console.SetEnabledAsync(kind, request.Enabled, cancellationToken);
        return NoContent();
    }

    /// <summary>Sends the administrator's own message to every seeker accepting announcements.</summary>
    [HttpPost("broadcast")]
    [ProducesResponseType(typeof(ConsoleActionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Broadcast([FromBody] ConsoleBroadcastRequestDto request, CancellationToken cancellationToken)
        => Ok(await _console.BroadcastAsync(request, cancellationToken));
}
