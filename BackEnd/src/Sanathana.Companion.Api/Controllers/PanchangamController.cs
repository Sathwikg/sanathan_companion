using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sanathana.Companion.Application.DTOs.Panchangams;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PanchangamController : ControllerBase
{
    private readonly IPanchangamService _service;

    public PanchangamController(IPanchangamService service) => _service = service;

    /// <summary>Stored Panchangam rows, filterable by year / region / date range / text.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PanchangamDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? year,
        [FromQuery] Guid? regionId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
        => Ok(await _service.GetAllAsync(year, regionId, from, to, search, cancellationToken));

    /// <summary>Years for which stored data exists, plus the selectable regions.</summary>
    [HttpGet("options")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOptions(CancellationToken cancellationToken)
        => Ok(new
        {
            years = await _service.GetStoredYearsAsync(cancellationToken),
            regions = await _service.GetRegionOptionsAsync(cancellationToken)
        });

    /// <summary>The stored row for a specific date + region.</summary>
    [HttpGet("by-date")]
    [ProducesResponseType(typeof(PanchangamDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByDate([FromQuery] DateOnly date, [FromQuery] Guid regionId, CancellationToken cancellationToken)
    {
        var dto = await _service.GetByDateAsync(date, regionId, cancellationToken);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>
    /// Compute a day's Panchangam for arbitrary coordinates — the endpoint the browser calls
    /// with the user's current geolocation. Nothing is stored; the same generic engine that
    /// seeds the database is used, so the result matches a stored row exactly.
    /// </summary>
    [HttpGet("compute")]
    [ProducesResponseType(typeof(PanchangamDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Compute(
        [FromQuery] double lat,
        [FromQuery] double lon,
        [FromQuery] DateOnly? date,
        [FromQuery] string? place,
        CancellationToken cancellationToken)
    {
        var d = date ?? DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));   // "today" in IST
        return Ok(await _service.ComputeAtLocationAsync(d, lat, lon, place, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PanchangamDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var dto = await _service.GetByIdAsync(id, cancellationToken);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Generate and store a whole year for one region (or all regions with coordinates).</summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("generate")]
    [ProducesResponseType(typeof(GenerateResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Generate([FromBody] GeneratePanchangamDto dto, CancellationToken cancellationToken)
        => Ok(await _service.GenerateAsync(dto, cancellationToken));
}
