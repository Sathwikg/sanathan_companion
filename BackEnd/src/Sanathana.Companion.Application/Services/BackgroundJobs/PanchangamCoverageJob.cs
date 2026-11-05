using Microsoft.Extensions.Logging;
using Sanathana.Companion.Application.DTOs.Panchangams;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Application.Services.BackgroundJobs;

/// <summary>
/// Keeps the stored Panchangam complete: this year and next, for every active region that has
/// coordinates. Runs shortly after start-up and once a day.
/// </summary>
/// <remarks>
/// The Panchangam table is derived data — every row is recomputable from a date and a region's
/// coordinates — so it is the one master that should never need re-entering by hand. When the
/// master data was rebuilt region by region the old rows went with the old region ids, and the
/// Panchangam page and the phone home went blank until an administrator pressed Generate for each
/// year. This job is that button, pressed by the server. Days already stored are skipped, so a
/// steady-state run is a handful of queries and no astronomy; a fresh database pays for the whole
/// computation once.
/// </remarks>
public sealed class PanchangamCoverageJob
{
    /// <summary>How many years past the current one are kept generated. One: festivals are entered a year ahead.</summary>
    public const int YearsAhead = 1;

    private readonly IPanchangamService _panchangam;
    private readonly ILogger<PanchangamCoverageJob> _logger;

    public PanchangamCoverageJob(IPanchangamService panchangam, ILogger<PanchangamCoverageJob> logger)
    {
        _panchangam = panchangam;
        _logger = logger;
    }

    /// <summary>Generates every missing day of the current IST year and the next, for all regions with coordinates.</summary>
    public async Task<PanchangamCoverageSummary> EnsureCoverageAsync(CancellationToken cancellationToken = default)
    {
        var todayIst = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));
        var summary = new PanchangamCoverageSummary();

        for (var year = todayIst.Year; year <= todayIst.Year + YearsAhead; year++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await _panchangam.GenerateAsync(new GeneratePanchangamDto { Year = year, Overwrite = false }, cancellationToken);
            summary.Years.Add(year);
            summary.Created += result.Created;
            summary.Skipped += result.Skipped;
            summary.Warnings.AddRange(result.Warnings);

            if (result.Created > 0)
                _logger.LogInformation("Panchangam coverage: generated {Created} day(s) for {Year} across {Regions} region(s)",
                    result.Created, year, result.Regions.Count);
        }

        foreach (var warning in summary.Warnings.Distinct())
            _logger.LogWarning("Panchangam coverage: {Warning}", warning);

        return summary;
    }
}

public sealed class PanchangamCoverageSummary
{
    public List<int> Years { get; } = new();
    public int Created { get; set; }
    public int Skipped { get; set; }
    public List<string> Warnings { get; } = new();
}
