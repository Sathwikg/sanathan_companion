using FluentValidation;
using Sanathana.Companion.Application.DTOs.Deities;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Exceptions;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Application.Services;

public class DeityService : IDeityService
{
    private readonly IUnitOfWork _uow;
    private readonly IValidator<CreateDeityDto> _createValidator;
    private readonly IValidator<UpdateDeityDto> _updateValidator;

    public DeityService(IUnitOfWork uow, IValidator<CreateDeityDto> createValidator, IValidator<UpdateDeityDto> updateValidator)
    {
        _uow = uow;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<IReadOnlyList<DeityDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await _uow.Deities.ListWithoutImageAsync(cancellationToken);
        return items.Select(ToDto).ToList();
    }

    public async Task<DeityDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var d = await _uow.Deities.GetByIdAsync(id, cancellationToken);
        return d is null ? null : ToDto(d);
    }

    public Task<(byte[]? Data, string? ContentType)> GetImageAsync(Guid id, CancellationToken cancellationToken = default)
        => _uow.Deities.GetImageAsync(id, cancellationToken);

    public async Task<Guid> CreateAsync(CreateDeityDto dto, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var name = dto.Name.Trim();
        if (await _uow.Deities.NameExistsAsync(name, null, cancellationToken))
            throw new ConflictException($"A deity named '{name}' already exists.");

        var entity = new Deity
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = Clean(dto.Description),
            WelcomeNote = Clean(dto.WelcomeNote),
            DeityType = dto.DeityType,
            Regions = Join(dto.Regions),
            Festivals = Join(dto.Festivals),
            Days = Join(dto.Days),
            IsActive = dto.IsActive
        };
        ApplyImage(entity, dto.ImageBase64);

        await _uow.Deities.AddAsync(entity, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateDeityDto dto, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var entity = await _uow.Deities.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Deity '{id}' was not found.");

        var name = dto.Name.Trim();
        if (await _uow.Deities.NameExistsAsync(name, id, cancellationToken))
            throw new ConflictException($"A deity named '{name}' already exists.");

        entity.Name = name;
        entity.Description = Clean(dto.Description);
        entity.WelcomeNote = Clean(dto.WelcomeNote);
        entity.DeityType = dto.DeityType;
        entity.Regions = Join(dto.Regions);
        entity.Festivals = Join(dto.Festivals);
        entity.Days = Join(dto.Days);
        entity.IsActive = dto.IsActive;

        if (!string.IsNullOrEmpty(dto.ImageBase64))
        {
            ApplyImage(entity, dto.ImageBase64);
        }
        else if (dto.RemoveImage)
        {
            entity.ImageData = null;
            entity.ImageContentType = null;
        }
        // otherwise keep the existing image

        _uow.Deities.Update(entity);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var entity = await _uow.Deities.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Deity '{id}' was not found.");
        entity.IsActive = isActive;
        _uow.Deities.Update(entity);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    public async Task<DeityFormOptionsDto> GetFormOptionsAsync(CancellationToken cancellationToken = default)
    {
        var regions = (await _uow.Regions.GetAllOrderedAsync(cancellationToken)).Where(r => r.IsActive).Select(r => r.Name).ToList();
        var festivals = (await _uow.Festivals.GetActiveNamesAsync(cancellationToken)).ToList();
        var days = (await _uow.Days.GetAllOrderedAsync(cancellationToken)).Where(d => d.IsActive).Select(d => d.Name).ToList();
        return new DeityFormOptionsDto { Regions = regions, Festivals = festivals, Days = days };
    }

    private static DeityDto ToDto(Deity d) => new()
    {
        Id = d.Id,
        Name = d.Name,
        Description = d.Description,
        WelcomeNote = d.WelcomeNote,
        DeityType = d.DeityType,
        HasImage = d.ImageContentType != null,
        Regions = Split(d.Regions),
        Festivals = Split(d.Festivals),
        Days = Split(d.Days),
        IsActive = d.IsActive
    };

    private static void ApplyImage(Deity entity, string? dataUri)
    {
        var parsed = ParseDataUri(dataUri);
        if (parsed is null) return;
        entity.ImageData = parsed.Value.Bytes;
        entity.ImageContentType = parsed.Value.ContentType;
    }

    private static (byte[] Bytes, string ContentType)? ParseDataUri(string? dataUri)
    {
        if (string.IsNullOrWhiteSpace(dataUri)) return null;
        var comma = dataUri.IndexOf(',');
        if (comma < 0) return null;

        var meta = dataUri[..comma];                 // e.g. "data:image/webp;base64"
        var b64 = dataUri[(comma + 1)..];
        var contentType = "image/webp";
        var colon = meta.IndexOf(':');
        var semi = meta.IndexOf(';');
        if (colon >= 0 && semi > colon) contentType = meta[(colon + 1)..semi];

        try { return (Convert.FromBase64String(b64), contentType); }
        catch { return null; }
    }

    private static string? Join(IEnumerable<string> values)
    {
        var cleaned = values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).ToList();
        return cleaned.Count == 0 ? null : string.Join(",", cleaned);
    }

    private static List<string> Split(string? csv)
        => string.IsNullOrWhiteSpace(csv) ? new() : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
