using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sanathana.Companion.Application.DTOs.AccessRights;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Api.Controllers;

/// <summary>Configures which forms each (non-Admin) role can access, per platform. Admin only.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AccessRightsController : ControllerBase
{
    private readonly IAccessRightsService _service;

    public AccessRightsController(IAccessRightsService service) => _service = service;

    /// <summary>Roles that access rights can be configured for (excludes Admin).</summary>
    [HttpGet("roles")]
    [ProducesResponseType(typeof(IReadOnlyList<AccessRoleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoles(CancellationToken cancellationToken)
        => Ok(await _service.GetAssignableRolesAsync(cancellationToken));

    /// <summary>The full access matrix (every form + this role's web/mobile access).</summary>
    [HttpGet("{roleId:int}")]
    [ProducesResponseType(typeof(AccessMatrixDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMatrix(int roleId, CancellationToken cancellationToken)
        => Ok(await _service.GetMatrixAsync(roleId, cancellationToken));

    /// <summary>Saves the role's access matrix.</summary>
    [HttpPut("{roleId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SaveMatrix(int roleId, [FromBody] SaveAccessRightsDto dto, CancellationToken cancellationToken)
    {
        await _service.SaveMatrixAsync(roleId, dto, cancellationToken);
        return NoContent();
    }
}
