using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Domain.Interfaces;

public interface IDeityRepository : IRepository<Deity>
{
    /// <summary>All deities, ordered by name, WITHOUT loading the image blob.</summary>
    Task<IReadOnlyList<Deity>> ListWithoutImageAsync(CancellationToken cancellationToken = default);

    /// <summary>Just the image bytes + content type for one deity.</summary>
    Task<(byte[]? Data, string? ContentType)> GetImageAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);
}
