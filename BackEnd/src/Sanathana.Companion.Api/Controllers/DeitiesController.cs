using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.Common.Authorization;
using Sanathana.Companion.Application.DTOs.Deities;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequiresModule(ModuleCodes.Deities)]
public class DeitiesController : ControllerBase
{
    private readonly IDeityService _service;

    public DeitiesController(IDeityService service) => _service = service;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DeityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        => Ok(await _service.GetAllAsync(cancellationToken));

    /// <summary>Region names, festival names and days for the form's multi-selects.</summary>
    [HttpGet("form-options")]
    [ProducesResponseType(typeof(DeityFormOptionsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFormOptions(CancellationToken cancellationToken)
        => Ok(await _service.GetFormOptionsAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DeityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var dto = await _service.GetByIdAsync(id, cancellationToken);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Serves the deity's profile picture blob. Public so it can be used directly in &lt;img&gt;.</summary>
    /// <remarks>
    /// Only published rows are served: deactivating a deity has to stop its picture being handed
    /// out, and nothing about the request says otherwise — these bytes are fetched by an
    /// &lt;img&gt; element, which carries no bearer token, so there is no privileged variant to
    /// branch on. An administrator previewing something they have just deactivated needs a
    /// credential the browser can actually attach.
    /// </remarks>
    [HttpGet("{id:guid}/image")]
    [AllowAnonymous]
    [EnableRateLimiting("media")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImage(Guid id, CancellationToken cancellationToken)
    {
        var (data, contentType) = await _service.GetImageAsync(id, cancellationToken: cancellationToken);
        if (data is null || data.Length == 0) return NotFound();
        Response.Headers.CacheControl = "public, max-age=3600";
        return File(data, contentType ?? "application/octet-stream");
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateDeityDto dto, CancellationToken cancellationToken)
    {
        var id = await _service.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDeityDto dto, CancellationToken cancellationToken)
    {
        await _service.UpdateAsync(id, dto, cancellationToken);
        return NoContent();
    }

    /// <summary>Activate / deactivate a deity.</summary>
    [Authorize(Roles = "Admin")]
    [HttpPut("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] UpdateDeityStatusDto dto, CancellationToken cancellationToken)
    {
        await _service.SetActiveAsync(id, dto.IsActive, cancellationToken);
        return NoContent();
    }
}
