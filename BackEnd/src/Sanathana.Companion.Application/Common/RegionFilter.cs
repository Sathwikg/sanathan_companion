using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Application.Common;

/// <summary>
/// Region matching for the devotee-facing content. Note the deliberate asymmetry in the data model:
/// <see cref="Deity.Regions"/> holds region NAMES while <see cref="Festival.Regions"/> holds region IDs.
/// In both cases an EMPTY mapping means "applies everywhere".
/// </summary>
/// <remarks>
/// The region is chosen by the caller — it arrives as a query parameter, and any seeker can change
/// their own with PUT profile/region — so this is a relevance filter, not a security boundary. That
/// is why the media endpoints do not apply it. If region ever has to restrict rather than merely
/// narrow, it has to become a server-derived property of the user first.
/// </remarks>
public static class RegionFilter
{
    /// <summary>True when the deity belongs to <paramref name="regionName"/> (or is mapped to no region).</summary>
    public static bool DeityInRegion(Deity deity, string? regionName)
    {
        if (string.IsNullOrWhiteSpace(regionName)) return true;   // no region chosen → everything

        var names = SplitNames(deity.Regions);
        return names.Count == 0 || names.Contains(regionName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>True when the festival applies to <paramref name="regionId"/> (or is mapped to no region).</summary>
    public static bool FestivalInRegion(Festival festival, Guid? regionId)
    {
        if (regionId is null) return true;

        var ids = CsvIds.Split(festival.Regions);
        return ids.Count == 0 || ids.Contains(regionId.Value);
    }

    /// <summary>
    /// True when a chant belongs to the region — i.e. at least one of its deities does.
    /// A chant mapped to no deity is treated as global.
    /// </summary>
    public static bool ChantInRegion(ChantConfig chant, string? regionName, IReadOnlyDictionary<Guid, Deity> deitiesById)
    {
        if (string.IsNullOrWhiteSpace(regionName)) return true;

        var deityIds = CsvIds.Split(chant.DeityIds);
        if (deityIds.Count == 0) return true;

        var known = deityIds.Where(deitiesById.ContainsKey).Select(id => deitiesById[id]).ToList();
        if (known.Count == 0) return true;

        return known.Any(d => DeityInRegion(d, regionName));
    }

    public static List<string> SplitNames(string? csv)
        => string.IsNullOrWhiteSpace(csv)
            ? new()
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
