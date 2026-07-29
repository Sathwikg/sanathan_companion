using Sanathana.Companion.Application.DTOs.Languages;

namespace Sanathana.Companion.Application.Interfaces;

public interface ILanguageService
{
    Task<IReadOnlyList<LanguageDto>> GetAllAsync(Guid? regionId, string? search, CancellationToken cancellationToken = default);
    Task<LanguageDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Every active region with the languages mapped to it.</summary>
    Task<IReadOnlyList<RegionLanguagesDto>> GetByRegionAsync(CancellationToken cancellationToken = default);

    Task<Guid> CreateAsync(CreateLanguageDto dto, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateLanguageDto dto, CancellationToken cancellationToken = default);
    Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
}
