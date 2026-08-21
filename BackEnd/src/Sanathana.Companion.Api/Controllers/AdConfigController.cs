using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.Common.Authorization;
using Sanathana.Companion.Application.DTOs.Ads;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Api.Controllers;

/// <summary>Admin: which forms show ads, and which single format each one shows.</summary>
[ApiController]
[Route("api/adconfig")]
[Authorize(Roles = "Admin")]
[RequiresModule(ModuleCodes.AdConfig)]
public class AdConfigController : ControllerBase
{
    private readonly IAdConfigService _service;

    public AdConfigController(IAdConfigService service) => _service = service;

    /// <summary>Settings, the format master, and every navigable form with its placement.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(AdConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
        => Ok(await _service.GetConfigAsync(cancellationToken));

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Save([FromBody] SaveAdConfigDto dto, CancellationToken cancellationToken)
    {
        await _service.SaveConfigAsync(dto, cancellationToken);
        return NoContent();
    }
}

/// <summary>What a client should show on one form. Read by the apps at run time.</summary>
[ApiController]
[Route("api/ads")]
[Authorize]
public class AdsController : ControllerBase
{
    private readonly IAdConfigService _service;

    public AdsController(IAdConfigService service) => _service = service;

    /// <summary>
    /// The ad decision for a form, already resolved for the calling platform.
    /// </summary>
    /// <remarks>
    /// Exempt from the module gate on purpose: this answers "should this screen show an ad", and
    /// whether the seeker may open that screen at all is decided by the screen's own endpoints. A
    /// role denied a form simply never asks. Gating it here would instead mean an ad silently
    /// vanishing for some roles and nobody knowing why.
    /// </remarks>
    [ModuleExempt]
    [HttpGet("slot/{menuModuleId:guid}")]
    [ProducesResponseType(typeof(AdSlotDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSlot(Guid menuModuleId, [FromQuery] string? platform, CancellationToken cancellationToken)
        => Ok(await _service.GetSlotAsync(menuModuleId, platform, cancellationToken));
}
