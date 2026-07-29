using Sanathana.Companion.Application.DTOs.Festivals;

namespace Sanathana.Companion.Application.Interfaces;

public interface IFestivalService
{
    Task<IReadOnlyList<FestivalDto>> GetByYearAsync(int year, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> GetYearsAsync(CancellationToken cancellationToken = default);
    Task<FestivalDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateFestivalDto dto, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateFestivalDto dto, CancellationToken cancellationToken = default);
    Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
}
