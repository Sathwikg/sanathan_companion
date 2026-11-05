using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sanathana.Companion.Api.Filters;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.Common.Authorization;
using Sanathana.Companion.Application.DTOs.Calendar;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Api.Controllers;

/// <summary>
/// The Hindu calendar: a month of tithis, moons, festivals and deities for a region, and the
/// signed-in seeker's own marks on it.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequiresModule(ModuleCodes.Calendar)]
public class CalendarController : ControllerBase
{
    private readonly ICalendarService _service;
    private readonly ICurrentUserService _currentUser;

    public CalendarController(ICalendarService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    /// <summary>One month for a region. A guest gets the shared content; a seeker also gets their marks.</summary>
    [GuestAccess]
    [HttpGet("month")]
    [ProducesResponseType(typeof(CalendarMonthDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMonth([FromQuery] int year, [FromQuery] int month, [FromQuery] Guid? regionId, CancellationToken cancellationToken)
        => Ok(await _service.GetMonthAsync(year, month, regionId, _currentUser.UserId, cancellationToken));

    /// <summary>The seeker's marks between two dates, yearly repeats expanded.</summary>
    [HttpGet("events")]
    [ProducesResponseType(typeof(IReadOnlyList<CalendarEventDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvents([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (userId is null) return Unauthorized();
        return Ok(await _service.GetEventsAsync(userId.Value, from, to, cancellationToken));
    }

    [HttpPost("events")]
    [ProducesResponseType(typeof(CalendarEventDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateEvent([FromBody] SaveCalendarEventDto dto, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (userId is null) return Unauthorized();
        var created = await _service.CreateEventAsync(userId.Value, dto, cancellationToken);
        return CreatedAtAction(nameof(GetEvents), new { from = created.Date, to = created.Date }, created);
    }

    [HttpPut("events/{id:guid}")]
    [ProducesResponseType(typeof(CalendarEventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEvent(Guid id, [FromBody] SaveCalendarEventDto dto, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (userId is null) return Unauthorized();
        return Ok(await _service.UpdateEventAsync(userId.Value, id, dto, cancellationToken));
    }

    [HttpDelete("events/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEvent(Guid id, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (userId is null) return Unauthorized();
        await _service.DeleteEventAsync(userId.Value, id, cancellationToken);
        return NoContent();
    }
}
