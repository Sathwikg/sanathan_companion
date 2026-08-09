using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Domain.Interfaces;

public interface IPujaProcessRepository
{
    // ---- Materials ----
    Task<IReadOnlyList<PujaMaterial>> GetMaterialsAsync(Guid pujaId, CancellationToken cancellationToken = default);
    Task<List<PujaMaterial>> GetMaterialsTrackedAsync(Guid pujaId, CancellationToken cancellationToken = default);
    Task AddMaterialAsync(PujaMaterial material, CancellationToken cancellationToken = default);
    void RemoveMaterial(PujaMaterial material);

    // ---- Steps ----
    /// <summary>Steps with every language's text, ordered by step number.</summary>
    Task<IReadOnlyList<PujaStep>> GetStepsWithTextsAsync(Guid pujaId, CancellationToken cancellationToken = default);
    Task<List<PujaStep>> GetStepsTrackedAsync(Guid pujaId, CancellationToken cancellationToken = default);
    Task AddStepAsync(PujaStep step, CancellationToken cancellationToken = default);
    void UpdateStep(PujaStep step);
    void RemoveStep(PujaStep step);

    Task AddStepTextAsync(PujaStepText text, CancellationToken cancellationToken = default);
    void UpdateStepText(PujaStepText text);
    void RemoveStepText(PujaStepText text);

    /// <summary>How many steps each puja has, so a list can show it without a query per row.</summary>
    Task<IReadOnlyDictionary<Guid, int>> GetStepCountsAsync(CancellationToken cancellationToken = default);
}
