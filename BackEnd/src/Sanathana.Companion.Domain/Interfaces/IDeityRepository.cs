using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Domain.Interfaces;

public interface IDeityRepository : IRepository<Deity>
{
    /// <summary>All deities, ordered by name, WITHOUT loading the image blob.</summary>
    Task<IReadOnlyList<Deity>> ListWithoutImageAsync(CancellationToken cancellationToken = default);

    /// <summary>Just the image bytes + content type for one deity.</summary>
    /// <summary>The stored bytes, or nulls when the row is missing or not published.</summary>
    /// <param name="includeInactive">
    /// Serves a deactivated row's bytes. Only an administrative caller should ask; there is no way
    /// to tell from the request itself, because these bytes are fetched by an &lt;img&gt; or
    /// &lt;audio&gt; element that carries no bearer token.
    /// </param>
    Task<(byte[]? Data, string? ContentType)> GetImageAsync(Guid id, bool includeInactive = false, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);
}
