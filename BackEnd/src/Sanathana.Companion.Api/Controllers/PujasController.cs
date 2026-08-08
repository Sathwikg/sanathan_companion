using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sanathana.Companion.Application.DTOs.Pujas;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PujasController : ControllerBase
{
    private readonly IPujaService _service;

    public PujasController(IPujaService service) => _service = service;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PujaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        => Ok(await _service.GetAllAsync(cancellationToken));

    /// <summary>Active festivals for the form's dropdown.</summary>
    [HttpGet("form-options")]
    [ProducesResponseType(typeof(PujaFormOptionsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFormOptions(CancellationToken cancellationToken)
        => Ok(await _service.GetFormOptionsAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PujaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var dto = await _service.GetByIdAsync(id, cancellationToken);
        return dto is null ? NotFound() : Ok(dto);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreatePujaDto dto, CancellationToken cancellationToken)
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
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePujaDto dto, CancellationToken cancellationToken)
    {
        await _service.UpdateAsync(id, dto, cancellationToken);
        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] SetStatusRequest request, CancellationToken cancellationToken)
    {
        await _service.SetStatusAsync(id, request.IsActive, cancellationToken);
        return NoContent();
    }

    public record SetStatusRequest(bool IsActive);
}
