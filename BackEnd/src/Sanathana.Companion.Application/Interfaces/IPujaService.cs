using Sanathana.Companion.Application.DTOs.Pujas;

namespace Sanathana.Companion.Application.Interfaces;

public interface IPujaService
{
    Task<IReadOnlyList<PujaDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PujaDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Active festivals for the form's dropdown.</summary>
    Task<PujaFormOptionsDto> GetFormOptionsAsync(CancellationToken cancellationToken = default);

    Task<Guid> CreateAsync(CreatePujaDto dto, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdatePujaDto dto, CancellationToken cancellationToken = default);
    Task SetStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
}
