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
    private readonly Sanathana.Companion.Application.Services.IPrivacyService _privacy;

    public AuditTelemetryController(IAuditService auditService, ICurrentUserService currentUser, IAuditRequestContext request,
        Sanathana.Companion.Application.Services.IPrivacyService privacy)
    {
        _auditService = auditService;
        _currentUser = currentUser;
        _request = request;
        _privacy = privacy;
    }

    /// <summary>A page visit by a signed-in user. Belongs to no form: every role's visits are recorded.</summary>
    [HttpPost("activity")]
    [ModuleExempt]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LogActivity([FromBody] LogActivityRequestDto dto, CancellationToken ct)
    {
        // Which screens a seeker opens, and for how long, is behavioural monitoring: kept only for a
        // seeker who opted in (Privacy & Your Data), and never for a guest, who cannot consent and
        // whose age is unknown (DPDP Act 2023, sections 6 and 9(3)). Answered 204 either way, so an
        // older app that still sends visits gets no error for it; the visit is simply not kept.
        if (_currentUser.UserId is { } userId && await _privacy.AllowsUsageAnalyticsAsync(userId, ct))
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
