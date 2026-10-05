using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.Common.Authorization;
using Sanathana.Companion.Application.DTOs.Audit;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Api.Controllers;

/// <summary>
/// Admin: the audit switches and the four logs. Admin-only like the other configuration forms,
/// because the logs hold every user's e-mail address and IP address.
/// </summary>
[ApiController]
[Route("api/audit")]
[Authorize(Roles = "Admin")]
[RequiresModule(ModuleCodes.AuditConfig)]
public class AuditConfigController : ControllerBase
{
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUser;

    public AuditConfigController(IAuditService auditService, ICurrentUserService currentUser)
    {
        _auditService = auditService;
        _currentUser = currentUser;
    }

    [HttpGet("config")]
    [ProducesResponseType(typeof(AuditConfigResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuditConfigResponseDto>> GetConfig(CancellationToken ct)
        => Ok(await _auditService.GetConfigAsync(ct));

    [HttpPut("config")]
    [ProducesResponseType(typeof(AuditConfigResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuditConfigResponseDto>> SaveConfig([FromBody] SaveAuditConfigDto dto, CancellationToken ct)
        => Ok(await _auditService.SaveConfigAsync(dto, ct));

    [HttpGet("sessions")]
    public async Task<ActionResult<PagedResultDto<AuditSessionLogDto>>> GetSessions([FromQuery] AuditLogQueryDto query, CancellationToken ct)
        => Ok(await _auditService.GetSessionsAsync(query, ct));

    [HttpGet("activities")]
    public async Task<ActionResult<PagedResultDto<AuditActivityLogDto>>> GetActivities([FromQuery] AuditLogQueryDto query, CancellationToken ct)
        => Ok(await _auditService.GetActivitiesAsync(query, ct));

    [HttpGet("data-logs")]
    public async Task<ActionResult<PagedResultDto<AuditDataLogDto>>> GetDataLogs([FromQuery] AuditLogQueryDto query, CancellationToken ct)
        => Ok(await _auditService.GetDataLogsAsync(query, ct));

    [HttpGet("errors")]
    public async Task<ActionResult<PagedResultDto<ErrorLogDto>>> GetErrors([FromQuery] AuditLogQueryDto query, CancellationToken ct)
        => Ok(await _auditService.GetErrorsAsync(query, ct));

    [HttpPut("errors/{id:guid}/resolve")]
    [ProducesResponseType(typeof(ErrorLogDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ErrorLogDto>> ResolveError(Guid id, CancellationToken ct)
    {
        var resolvedBy = _currentUser.Email ?? _currentUser.UserId?.ToString() ?? "Admin";
        var resolved = await _auditService.ResolveErrorAsync(id, resolvedBy, ct);
        return resolved is null ? NotFound() : Ok(resolved);
    }
}
