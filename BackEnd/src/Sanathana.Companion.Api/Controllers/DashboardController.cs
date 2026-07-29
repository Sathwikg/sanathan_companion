using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sanathana.Companion.Application.DTOs.Dashboard;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboard;

    public DashboardController(IDashboardService dashboard) => _dashboard = dashboard;

    /// <summary>User dashboard greeting. Requires a valid JWT.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(DashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Get()
    {
        var fullName = User.FindFirst("fullName")?.Value ?? "Seeker";
        var seekerName = User.FindFirst("seekerName")?.Value;
        var role = User.FindFirst("role")?.Value
                   ?? User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                   ?? string.Empty;

        var greetingName = string.IsNullOrWhiteSpace(seekerName) ? fullName : seekerName;

        var dto = new DashboardDto
        {
            FullName = fullName,
            SeekerName = string.IsNullOrWhiteSpace(seekerName) ? null : seekerName,
            Role = role,
            Message = $"🕉️ Namaste, {greetingName}! Welcome to your Sanathana Companion."
        };

        return Ok(dto);
    }

    /// <summary>Administrator overview: community size and sadhana engagement. Admin only.</summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("admin")]
    [ProducesResponseType(typeof(AdminDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAdmin(CancellationToken cancellationToken)
        => Ok(await _dashboard.GetAdminStatsAsync(cancellationToken));

    /// <summary>Today's Bhakti: today's deity/deities and the sadhana configured for each,
    /// optionally limited to the caller's selected region.</summary>
    [HttpGet("today-bhakti")]
    [ProducesResponseType(typeof(TodayBhaktiDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> TodayBhakti([FromQuery] Guid? regionId, CancellationToken cancellationToken)
        => Ok(await _dashboard.GetTodayBhaktiAsync(regionId, cancellationToken));

    /// <summary>Time-configured prayers ranked by relevance to the current time,
    /// optionally limited to the caller's selected region.</summary>
    [HttpGet("prayers")]
    [ProducesResponseType(typeof(PrayersDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Prayers([FromQuery] Guid? regionId, CancellationToken cancellationToken)
        => Ok(await _dashboard.GetPrayersAsync(regionId, cancellationToken));
}
