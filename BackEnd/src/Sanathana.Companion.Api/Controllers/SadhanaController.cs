using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sanathana.Companion.Application.DTOs.Sadhana;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SadhanaController : ControllerBase
{
    private readonly ISadhanaService _service;

    public SadhanaController(ISadhanaService service) => _service = service;

    /// <summary>Today's recommended chants (day + deity, festival override), sessions and streak.</summary>
    [HttpGet("today")]
    [ProducesResponseType(typeof(SadhanaTodayDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Today([FromQuery] Guid? regionId, CancellationToken cancellationToken)
        => Ok(await _service.GetTodayAsync(regionId, cancellationToken));

    /// <summary>All active chants for the search tab, with the user's progress today.
    /// Optionally limited to one region.</summary>
    [HttpGet("chants")]
    [ProducesResponseType(typeof(IReadOnlyList<SadhanaChantDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Chants([FromQuery] string? search, [FromQuery] Guid? regionId, CancellationToken cancellationToken)
        => Ok(await _service.GetChantsAsync(search, regionId, cancellationToken));

    /// <summary>Full chant detail plus today's progress, for the japa screen.</summary>
    [HttpGet("chants/{id:guid}")]
    [ProducesResponseType(typeof(SadhanaChantDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Chant(Guid id, CancellationToken cancellationToken)
    {
        var dto = await _service.GetChantAsync(id, cancellationToken);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Record the japa count for a chant today (absolute total). Updates malas + streak.</summary>
    [HttpPost("log")]
    [ProducesResponseType(typeof(LogCountResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Log([FromBody] LogCountDto dto, CancellationToken cancellationToken)
        => Ok(await _service.LogCountAsync(dto, cancellationToken));

    [HttpGet("streak")]
    [ProducesResponseType(typeof(SadhanaStreakDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Streak(CancellationToken cancellationToken)
        => Ok(await _service.GetStreakAsync(cancellationToken));
}
