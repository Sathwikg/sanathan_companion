using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sanathana.Companion.Api.Filters;
using Microsoft.AspNetCore.RateLimiting;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.Common.Authorization;
using Sanathana.Companion.Application.DTOs.Wallpapers;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequiresModule(ModuleCodes.Wallpapers)]
public class WallpapersController : ControllerBase
{
    private readonly IWallpaperService _service;

    public WallpapersController(IWallpaperService service) => _service = service;

    /// <summary>
    /// Deities for the picker. <paramref name="onlyWithWallpapers"/> is what the download screen
    /// uses, so a seeker is never offered a deity with an empty gallery.
    /// </summary>
    [RequiresModule(ModuleCodes.Wallpapers, ModuleCodes.WallpapersDownload)]
    [HttpGet("deities")]
    [ProducesResponseType(typeof(IReadOnlyList<WallpaperDeityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDeities([FromQuery] bool onlyWithWallpapers = false, CancellationToken cancellationToken = default)
        => Ok(await _service.GetDeitiesAsync(onlyWithWallpapers, cancellationToken));

    [RequiresModule(ModuleCodes.Wallpapers, ModuleCodes.WallpapersDownload)]
    [HttpGet("deity/{deityId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<WallpaperDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByDeity(Guid deityId, [FromQuery] bool activeOnly = true, CancellationToken cancellationToken = default)
        => Ok(await _service.GetByDeityAsync(deityId, activeOnly, cancellationToken));

    [RequiresModule(ModuleCodes.Wallpapers, ModuleCodes.WallpapersDownload)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(WallpaperDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var dto = await _service.GetByIdAsync(id, cancellationToken);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Serves the image for display. Anonymous so it can be used directly in &lt;img&gt;.</summary>
    /// <remarks>Published rows only; see DeitiesController.GetImage for why there is no admin variant.</remarks>
    [HttpGet("{id:guid}/image")]
    [AllowAnonymous]
    [MediaTicket]
    [EnableRateLimiting("media")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImage(Guid id, CancellationToken cancellationToken)
    {
        var (data, contentType, _) = await _service.GetImageAsync(id, cancellationToken: cancellationToken);
        if (data is null || data.Length == 0) return NotFound();

        Response.Headers.CacheControl = "private, max-age=21600";   // one ticket window
        return File(data, contentType ?? "application/octet-stream");
    }

    /// <summary>
    /// Same bytes as the image endpoint, but with Content-Disposition: attachment so the browser
    /// saves it instead of navigating to it. A separate route rather than a flag, so the display
    /// URL stays cacheable as an image.
    /// </summary>
    [HttpGet("{id:guid}/download")]
    [AllowAnonymous]
    [MediaTicket]
    [EnableRateLimiting("media")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var (data, contentType, title) = await _service.GetImageAsync(id, cancellationToken: cancellationToken);
        if (data is null || data.Length == 0) return NotFound();

        return File(data, contentType ?? "application/octet-stream", BuildFileName(title, contentType, id));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ProducesResponseType(typeof(WallpaperUploadResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreateWallpapersDto dto, CancellationToken cancellationToken)
        => Ok(await _service.CreateAsync(dto, cancellationToken));

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWallpaperDto dto, CancellationToken cancellationToken)
    {
        await _service.UpdateAsync(id, dto, cancellationToken);
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

    /// <summary>
    /// A safe download filename. The stored title is admin-supplied text that may contain path
    /// separators, quotes or newlines, any of which would let it tamper with the
    /// Content-Disposition header, so only a conservative set of characters survives.
    /// </summary>
    private static string BuildFileName(string? title, string? contentType, Guid id)
    {
        var ext = contentType switch
        {
            "image/webp" => ".webp",
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            _ => ".img"
        };

        var chars = (title ?? string.Empty)
            .Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : (c == ' ' ? '-' : '\0'))
            .Where(c => c != '\0')
            .ToArray();

        var stem = new string(chars).Trim('-');
        if (stem.Length == 0) stem = $"wallpaper-{id:N}";
        if (stem.Length > 60) stem = stem[..60];

        return stem + ext;
    }
}
