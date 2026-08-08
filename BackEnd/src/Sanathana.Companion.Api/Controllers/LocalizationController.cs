using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sanathana.Companion.Application.DTOs.Localization;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Api.Controllers;

/// <summary>
/// Serves the translation bundle every client loads, and the admin-only editing surface behind
/// the Language Configs screen.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class LocalizationController : ControllerBase
{
    private readonly ILocalizationService _service;

    public LocalizationController(ILocalizationService service) => _service = service;

    /// <summary>Languages the user can switch into. Anonymous so the login screen can be localized.</summary>
    [AllowAnonymous]
    [HttpGet("locales")]
    [ProducesResponseType(typeof(IReadOnlyList<LocaleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLocales(CancellationToken cancellationToken)
        => Ok(await _service.GetLocalesAsync(cancellationToken));

    /// <summary>
    /// The merged label + entity bundle for a language. Anonymous for the same reason as
    /// <see cref="GetLocales"/>; it contains only display text, no user data.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("bundle/{code}")]
    [ProducesResponseType(typeof(LocalizationBundleDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBundle(string code, CancellationToken cancellationToken)
        => Ok(await _service.GetBundleAsync(code, cancellationToken));

    // ---------------- admin editing surface ----------------

    [Authorize(Roles = "Admin")]
    [HttpGet("labels/{languageId:guid}")]
    [ProducesResponseType(typeof(LabelEditorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLabels(Guid languageId, CancellationToken cancellationToken)
        => Ok(await _service.GetLabelEditorAsync(languageId, cancellationToken));

    [Authorize(Roles = "Admin")]
    [HttpPut("labels/{languageId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SaveLabels(Guid languageId, [FromBody] SaveLabelsDto dto, CancellationToken cancellationToken)
    {
        await _service.SaveLabelsAsync(languageId, dto, cancellationToken);
        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("forms/{languageId:guid}")]
    [ProducesResponseType(typeof(LanguageFormMatrixDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetForms(Guid languageId, CancellationToken cancellationToken)
        => Ok(await _service.GetFormMatrixAsync(languageId, cancellationToken));

    [Authorize(Roles = "Admin")]
    [HttpPut("forms/{languageId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SaveForms(Guid languageId, [FromBody] SaveLanguageFormsDto dto, CancellationToken cancellationToken)
    {
        await _service.SaveFormMatrixAsync(languageId, dto, cancellationToken);
        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("entities/{languageId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<EntityTranslationRowDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEntityRows(Guid languageId, CancellationToken cancellationToken)
        => Ok(await _service.GetEntityRowsAsync(languageId, cancellationToken));

    [Authorize(Roles = "Admin")]
    [HttpPut("entities/{languageId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SaveEntityRows(Guid languageId, [FromBody] SaveEntityTranslationsDto dto, CancellationToken cancellationToken)
    {
        await _service.SaveEntityRowsAsync(languageId, dto, cancellationToken);
        return NoContent();
    }

    // ---------------- all languages in one grid ----------------

    /// <summary>
    /// Every language side by side for one scope. Omit <paramref name="scope"/> to get just the
    /// list of scopes (forms + shared sections) for the picker.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("matrix")]
    [ProducesResponseType(typeof(TranslationMatrixDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMatrix([FromQuery] string? scope, CancellationToken cancellationToken)
        => Ok(await _service.GetMatrixAsync(scope, cancellationToken));

    /// <summary>Saves many keys across many languages in one call.</summary>
    [Authorize(Roles = "Admin")]
    [HttpPut("matrix")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SaveMatrix([FromBody] SaveMatrixDto dto, CancellationToken cancellationToken)
    {
        await _service.SaveMatrixAsync(dto, cancellationToken);
        return NoContent();
    }

    /// <summary>DB-driven content (menu, deity, chant and region names) across every language.</summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("entity-matrix")]
    [ProducesResponseType(typeof(EntityMatrixDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEntityMatrix(CancellationToken cancellationToken)
        => Ok(await _service.GetEntityMatrixAsync(cancellationToken));

    [Authorize(Roles = "Admin")]
    [HttpPut("entity-matrix")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SaveEntityMatrix([FromBody] SaveEntityMatrixDto dto, CancellationToken cancellationToken)
    {
        await _service.SaveEntityMatrixAsync(dto, cancellationToken);
        return NoContent();
    }

    // ---------------- shared term dictionary (translates DB text) ----------------

    /// <summary>One page of the dictionary, every language side by side.</summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("dictionary")]
    [ProducesResponseType(typeof(DictionaryPageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDictionary(
        [FromServices] IDictionaryService dictionary,
        [FromQuery] string? category,
        [FromQuery] string? search,
        [FromQuery] bool missingOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
        => Ok(await dictionary.GetPageAsync(category, search, missingOnly, page, pageSize, cancellationToken));

    [Authorize(Roles = "Admin")]
    [HttpPut("dictionary")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SaveDictionary(
        [FromServices] IDictionaryService dictionary,
        [FromBody] SaveDictionaryDto dto,
        CancellationToken cancellationToken)
    {
        await dictionary.SaveAsync(dto, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Scans the registered columns (and anything the app failed to translate at runtime) for
    /// vocabulary the dictionary does not know yet. This is what keeps it current as data grows.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("dictionary/harvest")]
    [ProducesResponseType(typeof(HarvestResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Harvest(
        [FromServices] ITranslationHarvestService harvest, CancellationToken cancellationToken)
        => Ok(await harvest.HarvestAsync(cancellationToken));

    /// <summary>Re-reads the shipped seed files, filling gaps without overwriting hand edits.</summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("import")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Import(CancellationToken cancellationToken)
        => Ok(new { written = await _service.ImportSeedFilesAsync(cancellationToken) });

    /// <summary>The language's current state as JSON files, ready to commit back to the repo.</summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("export/{languageId:guid}")]
    [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Export(Guid languageId, CancellationToken cancellationToken)
        => Ok(await _service.ExportJsonAsync(languageId, cancellationToken));
}
