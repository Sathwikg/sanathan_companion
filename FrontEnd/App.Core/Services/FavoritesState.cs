namespace App.Core.Services;

/// <summary>App-wide cache of the current user's favorite ids so heart toggles across the app
/// stay in sync without each component re-fetching. Loaded once per session.</summary>
public class FavoritesState : IUserSessionState
{
    private readonly IApiClient _api;
    private readonly HashSet<Guid> _chants = new();
    private readonly HashSet<Guid> _deities = new();
    private Task? _loadTask;

    /// <summary>Bumped on every <see cref="Reset"/> so a load started by a previous user
    /// cannot write its results after someone else has signed in.</summary>
    private int _generation;

    public FavoritesState(IApiClient api) => _api = api;

    /// <summary>Raised whenever a favorite is toggled, so subscribed components can re-render.</summary>
    public event Action? OnChanged;

    /// <summary>Loads the favorite ids once; safe to call from many components concurrently.</summary>
    public Task EnsureLoadedAsync() => _loadTask ??= LoadAsync();

    private async Task LoadAsync()
    {
        var generation = _generation;
        try
        {
            var ids = await _api.GetFavoriteIdsAsync();
            if (generation != _generation) return;   // another user signed in mid-flight

            foreach (var id in ids.ChantIds) _chants.Add(id);
            foreach (var id in ids.DeityIds) _deities.Add(id);
        }
        catch
        {
            // Leave the caches empty on failure; toggles still work against the server.
        }
    }

    public bool IsChant(Guid id) => _chants.Contains(id);
    public bool IsDeity(Guid id) => _deities.Contains(id);

    public Task<bool> ToggleChantAsync(Guid id) => ToggleAsync("Chant", id, _chants);
    public Task<bool> ToggleDeityAsync(Guid id) => ToggleAsync("Deity", id, _deities);

    /// <summary>Clears the cache when the signed-in user changes, so favorites never leak between users.</summary>
    public void Reset()
    {
        _generation++;          // abandons any load still in flight for the previous user
        _loadTask = null;
        _chants.Clear();
        _deities.Clear();
    }

    private async Task<bool> ToggleAsync(string type, Guid id, HashSet<Guid> cache)
    {
        var (ok, isFavorite, _) = await _api.ToggleFavoriteAsync(type, id);
        if (ok)
        {
            if (isFavorite) cache.Add(id); else cache.Remove(id);
            OnChanged?.Invoke();
        }
        return isFavorite;
    }
}
