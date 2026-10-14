using Microsoft.Extensions.Logging;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Application.Services.BackgroundJobs;

/// <summary>
/// Background job for data synchronization tasks.
/// </summary>
/// <remarks>
/// This job handles periodic data synchronization such as:
/// - Festival data updates from external sources
/// - Panchangam pre-computation for popular regions
/// - Localization dictionary harvesting
/// - Search index updates (if applicable)
/// </remarks>
public sealed class DataSyncJob
{
    private readonly IUnitOfWork _uow;
    private readonly IPanchangamService _panchangamService;
    private readonly ILocalizationService _localizationService;
    private readonly ITranslationHarvestService _translationHarvestService;
    private readonly ILogger<DataSyncJob> _logger;

    public DataSyncJob(
        IUnitOfWork uow,
        IPanchangamService panchangamService,
        ILocalizationService localizationService,
        ITranslationHarvestService translationHarvestService,
        ILogger<DataSyncJob> logger)
    {
        _uow = uow;
        _panchangamService = panchangamService;
        _localizationService = localizationService;
        _translationHarvestService = translationHarvestService;
        _logger = logger;
    }

    /// <summary>
    /// Runs all data synchronization tasks.
    /// </summary>
    public async Task<DataSyncSummary> RunAllSyncAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting data sync job");

        var summary = new DataSyncSummary();

        try
        {
            summary.HarvestedTranslations = await HarvestTranslationsAsync(cancellationToken);
            summary.PrecomputedPanchangams = await PrecomputePopularPanchangamsAsync(cancellationToken);
            summary.SyncedFestivals = await SyncFestivalDataAsync(cancellationToken);

            _logger.LogInformation(
                "Data sync job completed: {Translations} translations, {Panchangams} panchangams, {Festivals} festivals",
                summary.HarvestedTranslations, summary.PrecomputedPanchangams, summary.SyncedFestivals);

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Data sync job failed");
            throw;
        }
    }

    /// <summary>
    /// Harvests missing translations from the application code.
    /// </summary>
    public async Task<int> HarvestTranslationsAsync(CancellationToken cancellationToken = default)
    {
        var result = await _translationHarvestService.HarvestAsync(cancellationToken);
        _logger.LogDebug("Harvested {Count} new translation keys", result.Added);
        return result.Added;
    }

    /// <summary>
    /// Pre-computes panchangam for active regions for the next 7 days.
    /// </summary>
    public async Task<int> PrecomputePopularPanchangamsAsync(CancellationToken cancellationToken = default)
    {
        // Get active regions
        var regions = await _uow.Regions.GetAllOrderedAsync(cancellationToken);
        var activeRegions = regions.Where(r => r.IsActive).Take(10).ToList();
        
        if (!activeRegions.Any()) return 0;

        var computed = 0;
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5)); // IST
        
        foreach (var region in activeRegions)
        {
            if (cancellationToken.IsCancellationRequested) break;

            for (int i = 0; i < 7; i++)
            {
                var date = today.AddDays(i);
                try
                {
                    // This will compute and cache the panchangam
                    await _panchangamService.GetByDateAsync(date, region.Id, cancellationToken);
                    computed++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to precompute panchangam for region {RegionId} on {Date}", region.Id, date);
                }
            }
        }

        _logger.LogDebug("Precomputed {Count} panchangams for active regions", computed);
        return computed;
    }

    /// <summary>
    /// Syncs festival data (placeholder for external festival API integration).
    /// </summary>
    public async Task<int> SyncFestivalDataAsync(CancellationToken cancellationToken = default)
    {
        // Placeholder for future external festival data integration
        // Could fetch from a Hindu calendar API and update local festival database
        await Task.CompletedTask;
        return 0;
    }

    /// <summary>
    /// Imports localization seed files (runs on startup but can be triggered manually).
    /// </summary>
    public async Task<int> ImportLocalizationSeedsAsync(CancellationToken cancellationToken = default)
    {
        var written = await _localizationService.ImportSeedFilesAsync(cancellationToken);
        _logger.LogDebug("Imported {Count} localization seed entries", written);
        return written;
    }
}

/// <summary>
/// Summary of data sync job results.
/// </summary>
public sealed class DataSyncSummary
{
    public int HarvestedTranslations { get; set; }
    public int PrecomputedPanchangams { get; set; }
    public int SyncedFestivals { get; set; }
    public int ImportedLocalizationSeeds { get; set; }

    public int Total => HarvestedTranslations + PrecomputedPanchangams + SyncedFestivals + ImportedLocalizationSeeds;
}