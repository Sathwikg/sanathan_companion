using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sanathana.Companion.Application.Common.Authorization;
using Sanathana.Companion.Application.DTOs.Audit;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Api.Controllers;

/// <summary>
/// What every client reports about itself: page visits and crashes. Both answer 204 at once; the
/// record is written in the background.
/// </summary>
/// <remarks>
/// Both sit behind the "telemetry" rate limit, so a script cannot fill the database through them,
/// and both cap and validate what they accept.
/// </remarks>
[ApiController]
[Route("api/audit")]
[EnableRateLimiting("telemetry")]
public class AuditTelemetryController : ControllerBase
{
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditRequestContext _request;

    public AuditTelemetryController(IAuditService auditService, ICurrentUserService currentUser, IAuditRequestContext request)
    {
        _auditService = auditService;
        _currentUser = currentUser;
        _request = request;
    }

    /// <summary>A page visit by a signed-in user. Belongs to no form: every role's visits are recorded.</summary>
    [HttpPost("activity")]
    [ModuleExempt]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LogActivity([FromBody] LogActivityRequestDto dto, CancellationToken ct)
    {
        await _auditService.RecordActivityAsync(dto, Caller(), ct);
        return NoContent();
    }

    /// <summary>
    /// A client-side crash. Anonymous because the sign-in screen can crash too, before there is
    /// anybody to authenticate.
    /// </summary>
    [HttpPost("errors")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LogError([FromBody] LogErrorRequestDto dto, CancellationToken ct)
    {
        await _auditService.RecordErrorAsync(dto, Caller(), ct);
        return NoContent();
    }

    private AuditCaller Caller() => new(
        _currentUser.UserId,
        _currentUser.Email,
        _request.SessionId,
        _request.IpAddress,
        _request.UserAgent,
        _request.Platform);
}
