using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Domain.Interfaces;

public interface IChantConfigRepository : IRepository<ChantConfig>
{
    /// <summary>Chant configs (without audio bytes), optionally filtered. Category is included.</summary>
    Task<IReadOnlyList<ChantConfig>> GetFilteredAsync(
        Guid? chantId,
        Guid? deityId,
        string? search,
        CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken = default);

    /// <summary>Loads the config together with its category, still without the audio bytes.</summary>
    Task<ChantConfig?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The audio bytes plus the content type recorded on the parent.</summary>
    /// <summary>The stored bytes, or nulls when the row is missing or not published.</summary>
    /// <param name="includeInactive">
    /// Serves a deactivated row's bytes. Only an administrative caller should ask; there is no way
    /// to tell from the request itself, because these bytes are fetched by an &lt;img&gt; or
    /// &lt;audio&gt; element that carries no bearer token.
    /// </param>
    Task<(byte[]? Data, string? ContentType, string? FileName)> GetAudioAsync(
        Guid id,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<ChantConfigAudio?> GetAudioEntityAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAudioAsync(ChantConfigAudio audio, CancellationToken cancellationToken = default);

    void RemoveAudio(ChantConfigAudio audio);

    // ---- per-language chant texts ----
    Task<IReadOnlyList<ChantLanguageConfig>> GetLanguageTextsAsync(Guid chantConfigId, CancellationToken cancellationToken = default);

    Task AddLanguageTextAsync(ChantLanguageConfig entity, CancellationToken cancellationToken = default);

    void UpdateLanguageText(ChantLanguageConfig entity);

    void RemoveLanguageText(ChantLanguageConfig entity);
}
