using Microsoft.Extensions.Caching.Memory;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Application.Panchangam;

namespace Sanathana.Companion.Application.Services;

/// <summary>
/// Caches the calculator's output — never the DTO.
/// </summary>
/// <remarks>
/// One compute is a few thousand VSOP87/ELP series evaluations plus two root searches, and the
/// endpoint is reachable by any signed-in seeker, so the same coordinates and date should not pay
/// for it twice.
/// <para>
/// It has to be <see cref="PanchangamDay"/> rather than the DTO: TranslationResultFilter is
/// global, and ObjectGraphTranslator rewrites DTO strings in place, so a shared DTO would be left
/// in the first caller's language for everyone who came after. PanchangamDay is init-only and
/// ToDto mints a fresh DTO per request.
/// </para>
/// </remarks>
public sealed class PanchangamComputeCache : IPanchangamComputeCache, IDisposable
{
    // A private instance rather than the DI-wide IMemoryCache: SizeLimit is per-cache, and setting
    // it on the shared one would make every other consumer throw for not declaring a Size.
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 20_000 });

    public PanchangamDay GetOrCompute(DateOnly date, double latitude, double longitude)
    {
        // Round, then compute FROM the rounded pair. Rounding only the key would make the answer
        // depend on whichever caller happened to warm the entry, so the same request would return
        // different sunrises cold and warm.
        var lat = Math.Round(latitude, 2);
        var lon = Math.Round(longitude, 2);

        return _cache.GetOrCreate((date, lat, lon), entry =>
        {
            entry.Size = 1;
            // Reclamation only. The value never changes; this just stops a long-lived process
            // holding every cell anyone ever asked for.
            entry.SlidingExpiration = TimeSpan.FromHours(12);
            return PanchangamCalculator.Compute(date, lat, lon);
        })!;
    }

    public void Dispose() => _cache.Dispose();
}
