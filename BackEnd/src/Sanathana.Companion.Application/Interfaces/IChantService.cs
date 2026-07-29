using Sanathana.Companion.Application.DTOs.Chants;

namespace Sanathana.Companion.Application.Interfaces;

public interface IChantService
{
    Task<IReadOnlyList<ChantDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ChantDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateChantDto dto, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateChantDto dto, CancellationToken cancellationToken = default);
    Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
}
