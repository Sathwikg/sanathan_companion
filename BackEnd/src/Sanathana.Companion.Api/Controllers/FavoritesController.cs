using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sanathana.Companion.Application.DTOs.Favorites;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Api.Controllers;

/// <summary>The signed-in user's favorite chants and gods.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FavoritesController : ControllerBase
{
    private readonly IFavoritesService _service;
    private readonly ICurrentUserService _currentUser;

    public FavoritesController(IFavoritesService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    /// <summary>The user's favorites, resolved for display.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(FavoritesDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (userId is null) return Unauthorized();
        return Ok(await _service.GetFavoritesAsync(userId.Value, cancellationToken));
    }

    /// <summary>The ids the user has favorited, so mark buttons can render filled.</summary>
    [HttpGet("ids")]
    [ProducesResponseType(typeof(FavoriteIdsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIds(CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (userId is null) return Unauthorized();
        return Ok(await _service.GetIdsAsync(userId.Value, cancellationToken));
    }

    /// <summary>Toggle a chant or god as favorite. Returns the new state.</summary>
    [HttpPost("toggle")]
    [ProducesResponseType(typeof(ToggleFavoriteResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Toggle([FromBody] ToggleFavoriteDto dto, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (userId is null) return Unauthorized();

        var isFavorite = await _service.ToggleAsync(userId.Value, dto.Type, dto.ItemId, cancellationToken);
        return Ok(new ToggleFavoriteResultDto { IsFavorite = isFavorite });
    }
}
