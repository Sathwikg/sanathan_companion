using Sanathana.Companion.Application.DTOs.ChantConfigs;

namespace Sanathana.Companion.Application.Interfaces;

public interface IChantConfigService
{
    Task<IReadOnlyList<ChantConfigListItemDto>> GetAllAsync(
        Guid? chantId,
        Guid? deityId,
        string? search,
        CancellationToken cancellationToken = default);

    Task<ChantConfigDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The stored bytes, or nulls when the row is missing or not published.</summary>
    Task<(byte[]? Data, string? ContentType, string? FileName)> GetAudioAsync(Guid id, bool includeInactive = false, CancellationToken cancellationToken = default);

    Task<ChantConfigFormOptionsDto> GetFormOptionsAsync(CancellationToken cancellationToken = default);

    Task<Guid> CreateAsync(CreateChantConfigDto dto, CancellationToken cancellationToken = default);

    Task UpdateAsync(Guid id, UpdateChantConfigDto dto, CancellationToken cancellationToken = default);

    Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
