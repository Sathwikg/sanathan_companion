using App.Core.Models;

namespace App.Core.Services;

/// <summary>
/// App-wide "which region am I viewing" state. Seeded from the user's default region (Profile)
/// and changed from the region selector in the top bar. Components subscribe to
/// <see cref="OnChanged"/> and re-query with <see cref="SelectedRegionId"/>.
///
/// Administrators may view every region at once; seekers always browse exactly one, so a
/// seeker without a stored default is given the first active region on sign-in.
/// </summary>
public class RegionState : IUserSessionState
{
    private const string AdminRole = "Admin";

    private readonly IApiClient _api;
    private Task? _loadTask;

    /// <summary>Bumped on every <see cref="Reset"/> so a load started by a previous user
    /// cannot write its results after someone else has signed in.</summary>
    private int _generation;

    public RegionState(IApiClient api) => _api = api;

    /// <summary>Active regions the user can choose from.</summary>
    public List<RegionOption> Regions { get; private set; } = new();

    /// <summary>The region currently being viewed; null = All regions (administrators only).</summary>
    public Guid? SelectedRegionId { get; private set; }

    /// <summary>True for administrators — only they get the "All Regions" choice.</summary>
    public bool AllowAllRegions { get; private set; }

    public string SelectedRegionName
        => Regions.FirstOrDefault(r => r.Id == SelectedRegionId)?.Name ?? "All Regions";

    /// <summary>Raised when the selection (or the region list) changes.</summary>
    public event Action? OnChanged;

    /// <summary>Loads the regions and applies the user's default region. Safe to call from many components.</summary>
    public Task EnsureLoadedAsync() => _loadTask ??= LoadAsync();

    private async Task LoadAsync()
    {
        var generation = _generation;

        List<RegionOption> regions;
        try { regions = await _api.GetRegionOptionsAsync(); }
        catch { regions = new(); }
        if (generation != _generation) return;   // another user signed in mid-flight

        bool allowAll = false;
        Guid? storedDefault = null;
        try
        {
            var profile = await _api.GetMyProfileAsync();
            allowAll = string.Equals(profile?.RoleName, AdminRole, StringComparison.OrdinalIgnoreCase);

            // Only honour the default if it is still an active region.
            if (profile?.DefaultRegionId is { } id && regions.Any(r => r.Id == id))
                storedDefault = id;
        }
        catch
        {
            // Treated as a seeker with no default.
        }
        if (generation != _generation) return;

        Regions = regions;
        AllowAllRegions = allowAll;
        SelectedRegionId = storedDefault;

        // A seeker must always be viewing some region — adopt the first one and remember it.
        if (SelectedRegionId is null && !AllowAllRegions && Regions.Count > 0)
        {
            SelectedRegionId = Regions[0].Id;
            try { await _api.SetDefaultRegionAsync(SelectedRegionId); } catch { /* view still works */ }
            if (generation != _generation) return;
        }

        OnChanged?.Invoke();
    }

    /// <summary>Changes the region being viewed (session-only; the default lives on the profile).</summary>
    public void Select(Guid? regionId)
    {
        // Seekers cannot clear the region back to "All Regions".
        if (regionId is null && !AllowAllRegions) return;
        if (SelectedRegionId == regionId) return;

        SelectedRegionId = regionId;
        OnChanged?.Invoke();
    }

    /// <summary>Called after the profile's default region is saved, so the selector follows it.</summary>
    public void ApplyDefault(Guid? regionId) => Select(regionId);

    /// <summary>Clears the cache when the signed-in user changes, so the next user reloads their own region.</summary>
    public void Reset()
    {
        _generation++;          // abandons any load still in flight for the previous user
        _loadTask = null;
        Regions = new();
        SelectedRegionId = null;
        AllowAllRegions = false;
    }
}
