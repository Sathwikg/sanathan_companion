using FluentValidation;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.DTOs.ChantConfigs;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Exceptions;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Application.Services;

public class ChantConfigService : IChantConfigService
{
    /// <summary>Audio cap. Kept well under Kestrel's ~28 MB default body limit once base64-inflated.</summary>
    private const long MaxAudioBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> AllowedAudioTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "audio/mpeg", "audio/mp3", "audio/wav", "audio/wave",
        "audio/x-wav", "audio/vnd.wave", "audio/ogg", "application/ogg"
    };

    private readonly IUnitOfWork _uow;
    private readonly IValidator<CreateChantConfigDto> _createValidator;
    private readonly IValidator<UpdateChantConfigDto> _updateValidator;

    public ChantConfigService(
        IUnitOfWork uow,
        IValidator<CreateChantConfigDto> createValidator,
        IValidator<UpdateChantConfigDto> updateValidator)
    {
        _uow = uow;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<IReadOnlyList<ChantConfigListItemDto>> GetAllAsync(
        Guid? chantId,
        Guid? deityId,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var items = await _uow.ChantConfigs.GetFilteredAsync(chantId, deityId, search, cancellationToken);
        var deityNames = await GetDeityNamesAsync(cancellationToken);

        return items.Select(c =>
        {
            var ids = SplitIds(c.DeityIds);
            return new ChantConfigListItemDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                ChantId = c.ChantId,
                ChantName = c.Chant?.Name ?? string.Empty,
                DeityIds = ids,
                DeityNames = ResolveNames(ids, deityNames),
                TextPreview = Preview(c.ChantText),
                HasAudio = c.AudioContentType != null,
                FromTime = c.FromTime,
                ToTime = c.ToTime,
                TimeDescription = c.TimeDescription,
                IsActive = c.IsActive
            };
        }).ToList();
    }

    public async Task<ChantConfigDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var c = await _uow.ChantConfigs.GetDetailAsync(id, cancellationToken);
        if (c is null) return null;

        var deityNames = await GetDeityNamesAsync(cancellationToken);
        var ids = SplitIds(c.DeityIds);

        var langNames = (await _uow.Languages.GetAllOrderedAsync(cancellationToken)).ToDictionary(l => l.Id, l => l.Name);
        var langTexts = (await _uow.ChantConfigs.GetLanguageTextsAsync(id, cancellationToken))
            .Select(t => new ChantLanguageTextDto
            {
                LanguageId = t.LanguageId,
                LanguageName = langNames.GetValueOrDefault(t.LanguageId, string.Empty),
                Text = t.Text
            }).ToList();

        return new ChantConfigDto
        {
            Id = c.Id,
            Name = c.Name,
            Description = c.Description,
            ChantId = c.ChantId,
            ChantName = c.Chant?.Name ?? string.Empty,
            DeityIds = ids,
            DeityNames = ResolveNames(ids, deityNames),
            ChantText = c.ChantText,
            HasAudio = c.AudioContentType != null,
            AudioFileName = c.AudioFileName,
            AudioContentType = c.AudioContentType,
            AudioSizeBytes = c.AudioSizeBytes,
            FromTime = c.FromTime,
            ToTime = c.ToTime,
            TimeDescription = c.TimeDescription,
            LanguageTexts = langTexts,
            IsActive = c.IsActive
        };
    }

    public Task<(byte[]? Data, string? ContentType, string? FileName)> GetAudioAsync(Guid id, CancellationToken cancellationToken = default)
        => _uow.ChantConfigs.GetAudioAsync(id, cancellationToken);

    public async Task<ChantConfigFormOptionsDto> GetFormOptionsAsync(CancellationToken cancellationToken = default)
    {
        var categories = (await _uow.Chants.GetAllOrderedAsync(cancellationToken))
            .Where(c => c.IsActive)
            .Select(c => new OptionDto { Id = c.Id, Name = c.Name })
            .ToList();

        var deities = (await _uow.Deities.ListWithoutImageAsync(cancellationToken))
            .Where(d => d.IsActive)
            .Select(d => new OptionDto { Id = d.Id, Name = d.Name })
            .ToList();

        return new ChantConfigFormOptionsDto { Categories = categories, Deities = deities };
    }

    public async Task<Guid> CreateAsync(CreateChantConfigDto dto, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var name = dto.Name.Trim();
        await EnsureCategoryExistsAsync(dto.ChantId, cancellationToken);

        if (await _uow.ChantConfigs.NameExistsAsync(name, null, cancellationToken))
            throw new ConflictException($"A chant named '{name}' already exists.");

        var entity = new ChantConfig
        {
            Id = Guid.NewGuid(),
            ChantId = dto.ChantId,
            Name = name,
            Description = Clean(dto.Description),
            DeityIds = JoinIds(dto.DeityIds),
            ChantText = HtmlSanitizer.Sanitize(dto.ChantText),
            FromTime = dto.FromTime,
            ToTime = dto.ToTime,
            TimeDescription = Clean(dto.TimeDescription),
            IsActive = dto.IsActive
        };

        await _uow.ChantConfigs.AddAsync(entity, cancellationToken);

        var audio = ParseAudio(dto.AudioBase64);
        if (audio is not null)
        {
            entity.AudioContentType = audio.Value.ContentType;
            entity.AudioFileName = Clean(dto.AudioFileName);
            entity.AudioSizeBytes = audio.Value.Bytes.LongLength;
            await _uow.ChantConfigs.AddAudioAsync(
                new ChantConfigAudio { ChantConfigId = entity.Id, Data = audio.Value.Bytes },
                cancellationToken);
        }

        await SyncLanguageTextsAsync(entity.Id, dto.LanguageTexts, cancellationToken);

        await _uow.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateChantConfigDto dto, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var entity = await _uow.ChantConfigs.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Chant '{id}' was not found.");

        var name = dto.Name.Trim();
        await EnsureCategoryExistsAsync(dto.ChantId, cancellationToken);

        if (await _uow.ChantConfigs.NameExistsAsync(name, id, cancellationToken))
            throw new ConflictException($"A chant named '{name}' already exists.");

        entity.ChantId = dto.ChantId;
        entity.Name = name;
        entity.Description = Clean(dto.Description);
        entity.DeityIds = JoinIds(dto.DeityIds);
        entity.ChantText = HtmlSanitizer.Sanitize(dto.ChantText);
        entity.FromTime = dto.FromTime;
        entity.ToTime = dto.ToTime;
        entity.TimeDescription = Clean(dto.TimeDescription);
        entity.IsActive = dto.IsActive;

        var newAudio = ParseAudio(dto.AudioBase64);
        if (newAudio is not null)
        {
            var existing = await _uow.ChantConfigs.GetAudioEntityAsync(id, cancellationToken);
            if (existing is null)
            {
                await _uow.ChantConfigs.AddAudioAsync(
                    new ChantConfigAudio { ChantConfigId = id, Data = newAudio.Value.Bytes },
                    cancellationToken);
            }
            else
            {
                existing.Data = newAudio.Value.Bytes;
            }

            entity.AudioContentType = newAudio.Value.ContentType;
            entity.AudioFileName = Clean(dto.AudioFileName);
            entity.AudioSizeBytes = newAudio.Value.Bytes.LongLength;
        }
        else if (dto.RemoveAudio)
        {
            var existing = await _uow.ChantConfigs.GetAudioEntityAsync(id, cancellationToken);
            if (existing is not null) _uow.ChantConfigs.RemoveAudio(existing);

            entity.AudioContentType = null;
            entity.AudioFileName = null;
            entity.AudioSizeBytes = null;
        }
        // otherwise keep the existing audio

        await SyncLanguageTextsAsync(id, dto.LanguageTexts, cancellationToken);

        _uow.ChantConfigs.Update(entity);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Writes the per-language chant texts: upserts entries that have content and removes ones
    /// that were cleared. Only known language ids are accepted and each text is sanitized.
    /// </summary>
    private async Task SyncLanguageTextsAsync(Guid chantConfigId, List<ChantLanguageTextDto> incoming, CancellationToken cancellationToken)
    {
        var known = (await _uow.Languages.GetAllOrderedAsync(cancellationToken)).Select(l => l.Id).ToHashSet();
        var existing = (await _uow.ChantConfigs.GetLanguageTextsAsync(chantConfigId, cancellationToken))
            .ToDictionary(x => x.LanguageId, x => x);

        foreach (var item in incoming)
        {
            if (item.LanguageId == Guid.Empty || !known.Contains(item.LanguageId)) continue;

            var sanitized = HtmlSanitizer.Sanitize(item.Text);
            bool isEmpty = string.IsNullOrWhiteSpace(HtmlSanitizer.ToPlainText(sanitized));

            existing.TryGetValue(item.LanguageId, out var row);

            if (isEmpty)
            {
                if (row is not null)
                {
                    _uow.ChantConfigs.RemoveLanguageText(row);
                    existing.Remove(item.LanguageId);
                }
                continue;
            }

            if (row is null)
            {
                await _uow.ChantConfigs.AddLanguageTextAsync(new ChantLanguageConfig
                {
                    Id = Guid.NewGuid(),
                    ChantConfigId = chantConfigId,
                    LanguageId = item.LanguageId,
                    Text = sanitized
                }, cancellationToken);
            }
            else
            {
                row.Text = sanitized;
                _uow.ChantConfigs.UpdateLanguageText(row);
            }
        }
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var entity = await _uow.ChantConfigs.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Chant '{id}' was not found.");
        entity.IsActive = isActive;
        _uow.ChantConfigs.Update(entity);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _uow.ChantConfigs.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Chant '{id}' was not found.");

        var audio = await _uow.ChantConfigs.GetAudioEntityAsync(id, cancellationToken);
        if (audio is not null) _uow.ChantConfigs.RemoveAudio(audio);

        _uow.ChantConfigs.Remove(entity);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureCategoryExistsAsync(Guid chantId, CancellationToken cancellationToken)
    {
        var category = await _uow.Chants.GetByIdAsync(chantId, cancellationToken);
        if (category is null) throw new BadRequestException("The selected chant category does not exist.");
    }

    private async Task<Dictionary<Guid, string>> GetDeityNamesAsync(CancellationToken cancellationToken)
        => (await _uow.Deities.ListWithoutImageAsync(cancellationToken)).ToDictionary(d => d.Id, d => d.Name);

    private static List<string> ResolveNames(List<Guid> ids, Dictionary<Guid, string> map)
        => ids.Where(map.ContainsKey).Select(id => map[id]).ToList();

    /// <summary>Strips markup and trims to a card-sized snippet.</summary>
    private static string Preview(string? html)
    {
        const int max = 180;
        var text = HtmlSanitizer.ToPlainText(html);
        return text.Length <= max ? text : text[..max].TrimEnd() + "…";
    }

    private static (byte[] Bytes, string ContentType)? ParseAudio(string? dataUri)
    {
        if (string.IsNullOrWhiteSpace(dataUri)) return null;

        var comma = dataUri.IndexOf(',');
        if (comma < 0) throw new BadRequestException("The audio file could not be read.");

        var meta = dataUri[..comma];                       // e.g. "data:audio/mpeg;base64"
        var b64 = dataUri[(comma + 1)..];

        var contentType = "audio/mpeg";
        var colon = meta.IndexOf(':');
        var semi = meta.IndexOf(';');
        if (colon >= 0 && semi > colon) contentType = meta[(colon + 1)..semi];

        if (!AllowedAudioTypes.Contains(contentType))
            throw new BadRequestException("Only MP3, WAV and OGG audio files are supported.");

        // Reject before allocating/decoding: base64 is ~4/3 the byte size, so cap the string length.
        if (b64.Length > (MaxAudioBytes / 3 * 4) + 4)
            throw new BadRequestException($"Audio must be {MaxAudioBytes / (1024 * 1024)} MB or smaller.");

        byte[] bytes;
        try { bytes = Convert.FromBase64String(b64); }
        catch { throw new BadRequestException("The audio file could not be read."); }

        if (bytes.LongLength == 0)
            throw new BadRequestException("The audio file is empty.");

        if (bytes.LongLength > MaxAudioBytes)
            throw new BadRequestException($"Audio must be {MaxAudioBytes / (1024 * 1024)} MB or smaller.");

        return (bytes, contentType);
    }

    private static string? JoinIds(IEnumerable<Guid> ids)
    {
        var cleaned = ids.Where(id => id != Guid.Empty).Distinct().Select(id => id.ToString()).ToList();
        return cleaned.Count == 0 ? null : string.Join(",", cleaned);
    }

    private static List<Guid> SplitIds(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return new();
        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                  .Select(v => Guid.TryParse(v, out var g) ? g : Guid.Empty)
                  .Where(g => g != Guid.Empty)
                  .ToList();
    }

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
