using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sanathana.Companion.Application.DTOs.ChantConfigs;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChantConfigsController : ControllerBase
{
    private readonly IChantConfigService _service;

    public ChantConfigsController(IChantConfigService service) => _service = service;

    /// <summary>Configured chants, optionally filtered by category, deity or free text.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ChantConfigListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? chantId,
        [FromQuery] Guid? deityId,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
        => Ok(await _service.GetAllAsync(chantId, deityId, search, cancellationToken));

    /// <summary>Chant categories and deities for the form's selects.</summary>
    [HttpGet("form-options")]
    [ProducesResponseType(typeof(ChantConfigFormOptionsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFormOptions(CancellationToken cancellationToken)
        => Ok(await _service.GetFormOptionsAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ChantConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var dto = await _service.GetByIdAsync(id, cancellationToken);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Streams the chant's audio. Public so it can be used directly in &lt;audio&gt;.</summary>
    [HttpGet("{id:guid}/audio")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAudio(Guid id, CancellationToken cancellationToken)
    {
        var (data, contentType, _) = await _service.GetAudioAsync(id, cancellationToken);
        if (data is null || data.Length == 0) return NotFound();
        Response.Headers.CacheControl = "public, max-age=3600";
        // Range processing lets the player seek without re-downloading.
        return File(data, contentType ?? "application/octet-stream", enableRangeProcessing: true);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateChantConfigDto dto, CancellationToken cancellationToken)
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
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateChantConfigDto dto, CancellationToken cancellationToken)
    {
        await _service.UpdateAsync(id, dto, cancellationToken);
        return NoContent();
    }

    /// <summary>Activate / deactivate a configured chant.</summary>
    [Authorize(Roles = "Admin")]
    [HttpPut("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] UpdateChantConfigStatusDto dto, CancellationToken cancellationToken)
    {
        await _service.SetActiveAsync(id, dto.IsActive, cancellationToken);
        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
