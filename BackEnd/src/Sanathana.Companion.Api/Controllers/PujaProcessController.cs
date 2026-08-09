using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sanathana.Companion.Api.Filters;
using Sanathana.Companion.Application.DTOs.Pujas;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PujaProcessController : ControllerBase
{
    private readonly IPujaProcessService _service;
    private readonly ICurrentUserService _currentUser;

    public PujaProcessController(IPujaProcessService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    private Guid RequireUserId() => _currentUser.UserId ?? throw new UnauthorizedAccessException();

    // ---- admin configuration ----

    [Authorize(Roles = "Admin")]
    [HttpGet("config/{pujaId:guid}")]
    [ProducesResponseType(typeof(PujaProcessConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConfig(Guid pujaId, CancellationToken cancellationToken)
        => Ok(await _service.GetConfigAsync(pujaId, cancellationToken));

    [Authorize(Roles = "Admin")]
    [HttpPut("config/{pujaId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SaveConfig(Guid pujaId, [FromBody] SavePujaProcessDto dto, CancellationToken cancellationToken)
    {
        await _service.SaveConfigAsync(pujaId, dto, cancellationToken);
        return NoContent();
    }

    // ---- user runtime ----

    /// <summary>Festivals with a configured puja; exactly one is flagged as current.</summary>
    [HttpGet("festivals")]
    [ProducesResponseType(typeof(IReadOnlyList<ProcessFestivalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFestivals(CancellationToken cancellationToken)
        => Ok(await _service.GetFestivalsAsync(RequireUserId(), cancellationToken));

    [HttpGet("festival/{festivalId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<ProcessPujaSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPujasForFestival(Guid festivalId, CancellationToken cancellationToken)
        => Ok(await _service.GetPujasForFestivalAsync(RequireUserId(), festivalId, cancellationToken));

    /// <summary>
    /// The process itself. Step wording is authored per language, so it is resolved here from the
    /// caller's chosen language rather than by the translation filter, which only knows the
    /// shared dictionary.
    /// </summary>
    [HttpGet("puja/{pujaId:guid}")]
    [ProducesResponseType(typeof(PujaProcessViewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProcess(Guid pujaId, CancellationToken cancellationToken)
    {
        var lang = Request.Headers[TranslationResultFilter.LanguageHeader].ToString();
        var dto = await _service.GetProcessAsync(RequireUserId(), pujaId, lang, cancellationToken);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost("step/{stepId:guid}/complete")]
    [ProducesResponseType(typeof(PujaProgressResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CompleteStep(Guid stepId, CancellationToken cancellationToken)
        => Ok(await _service.CompleteStepAsync(RequireUserId(), stepId, cancellationToken));

    [HttpDelete("step/{stepId:guid}/complete")]
    [ProducesResponseType(typeof(PujaProgressResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UndoStep(Guid stepId, CancellationToken cancellationToken)
        => Ok(await _service.UndoStepAsync(RequireUserId(), stepId, cancellationToken));

    /// <summary>Clears progress so a recurring puja can be performed again.</summary>
    [HttpPost("puja/{pujaId:guid}/reset")]
    [ProducesResponseType(typeof(PujaProgressResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reset(Guid pujaId, CancellationToken cancellationToken)
        => Ok(await _service.ResetAsync(RequireUserId(), pujaId, cancellationToken));
}
