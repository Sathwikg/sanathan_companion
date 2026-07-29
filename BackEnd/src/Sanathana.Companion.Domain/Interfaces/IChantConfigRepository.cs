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
    Task<(byte[]? Data, string? ContentType, string? FileName)> GetAudioAsync(
        Guid id,
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
