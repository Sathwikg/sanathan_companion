using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.Common.Authorization;
using Sanathana.Companion.Application.DTOs.Panchangams;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequiresModule(ModuleCodes.Panchangam)]
public class PanchangamController : ControllerBase
{
    private readonly IPanchangamService _service;

    public PanchangamController(IPanchangamService service) => _service = service;

    /// <summary>
    /// A page of stored Panchangam rows, filterable by year / region / date range / text.
    /// </summary>
    /// <remarks>Returns an envelope, not a bare array — the response used to be unbounded.</remarks>
    [RequiresModule(ModuleCodes.Panchangam, ModuleCodes.Dashboard, ModuleCodes.MobileDashboard)]
    [HttpGet]
    [ProducesResponseType(typeof(PanchangamPageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? year,
        [FromQuery] Guid? regionId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 200,
        CancellationToken cancellationToken = default)
        => Ok(await _service.GetAllAsync(year, regionId, from, to, search, page, pageSize, cancellationToken));

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
    /// Compute a day's Panchangam for arbitrary coordinates — the endpoint the app calls with the
    /// seeker's current geolocation. Nothing is stored; the same generic engine that seeds the
    /// database is used, so the result matches a stored row exactly.
    /// </summary>
    /// <remarks>
    /// A POST for a read, deliberately: the arguments are somebody's location, and a query string
    /// is copied into the access log of every proxy on the way. The clients already coarsen the
    /// fix to two decimal places before it is sent — see GeoPrecision — so nothing sharper than a
    /// neighbourhood arrives here in the first place.
    /// </remarks>
    [EnableRateLimiting("compute")]
    [HttpPost("compute")]
    [ProducesResponseType(typeof(PanchangamDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Compute([FromBody] ComputePanchangamDto dto, CancellationToken cancellationToken)
    {
        var d = dto.Date ?? DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));   // "today" in IST
        return Ok(await _service.ComputeAtLocationAsync(d, dto.Latitude, dto.Longitude, dto.Place, cancellationToken));
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
    // Throttled too, and it is the heavier of the two by far: one call computes 365 days per
    // region inside a single request.
    [EnableRateLimiting("compute")]
    [Authorize(Roles = "Admin")]
    [HttpPost("generate")]
    [ProducesResponseType(typeof(GenerateResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Generate([FromBody] GeneratePanchangamDto dto, CancellationToken cancellationToken)
        => Ok(await _service.GenerateAsync(dto, cancellationToken));
}
