using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Domain.Interfaces;

public interface ILanguageRepository : IRepository<Language>
{
    Task<IReadOnlyList<Language>> GetAllOrderedAsync(CancellationToken cancellationToken = default);

    /// <summary>Languages optionally narrowed to one region, or matched by free text.</summary>
    Task<IReadOnlyList<Language>> GetFilteredAsync(
        Guid? regionId,
        string? search,
        CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);
}
