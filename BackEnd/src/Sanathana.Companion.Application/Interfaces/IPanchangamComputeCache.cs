using Sanathana.Companion.Application.Panchangam;

namespace Sanathana.Companion.Application.Interfaces;

/// <summary>
/// Memoises the astronomical calculation behind the compute endpoint.
/// </summary>
public interface IPanchangamComputeCache
{
    /// <summary>
    /// The day's astronomy for a location, computed once per rounded cell and date.
    /// </summary>
    PanchangamDay GetOrCompute(DateOnly date, double latitude, double longitude);
}
