using App.Core.Auth;
using App.Core.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace App.Core.Services;

/// <summary>
/// Reports how long the signed-in user spent on each page, once they leave it.
/// </summary>
/// <remarks>
/// <para>
/// Only the route and the duration are sent. The server works out which form the route belongs to
/// from the Modules table, so a new form needs no change here, and it dates the visit by its own
/// clock, so a phone with a wrong clock cannot misdate the log.
/// </para>
/// <para>
/// Time with the app in the background does not count: the visit is closed when the app is
/// backgrounded and a new one starts when it returns.
/// </para>
/// </remarks>
public sealed class NavigationTrackerService : IDisposable
{
    private readonly NavigationManager _navigationManager;
    private readonly IApiClient _apiClient;
    private readonly ITokenStore _tokens;
    private readonly IAppLifecycle _lifecycle;

    private string? _currentRoute;
    private DateTime? _enteredAtUtc;
    private bool _initialized;
    private bool _disposed;

    public NavigationTrackerService(NavigationManager navigationManager, IApiClient apiClient, ITokenStore tokens, IAppLifecycle lifecycle)
    {
        _navigationManager = navigationManager;
        _apiClient = apiClient;
        _tokens = tokens;
        _lifecycle = lifecycle;
    }

    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        _currentRoute = CleanRoute(_navigationManager.Uri);
        _enteredAtUtc = DateTime.UtcNow;

        _navigationManager.LocationChanged += OnLocationChanged;
        _lifecycle.Changed += OnLifecycleChanged;
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
        => FlushCurrent(CleanRoute(e.Location));

    private void OnLifecycleChanged()
    {
        if (_lifecycle.IsActive)
        {
            // Back in front: start timing the page again from now.
            _enteredAtUtc = DateTime.UtcNow;
        }
        else
        {
            // Backgrounded: close the visit, but remember the page for when the app returns.
            var route = _currentRoute;
            FlushCurrent(route);
            _enteredAtUtc = null;
        }
    }

    private void FlushCurrent(string? nextRoute)
    {
        if (_enteredAtUtc is { } entered && !string.IsNullOrEmpty(_currentRoute))
        {
            var seconds = (int)(DateTime.UtcNow - entered).TotalSeconds;

            // A redirect or a bounce through a page is not a visit.
            if (seconds >= 1)
                _ = SendAsync(new LogActivityRequestModel { RoutePath = _currentRoute, TimeSpentSeconds = seconds });
        }

        _currentRoute = nextRoute;
        _enteredAtUtc = nextRoute is null ? null : DateTime.UtcNow;
    }

    private async Task SendAsync(LogActivityRequestModel visit)
    {
        try
        {
            // Visits are recorded for signed-in users only; the endpoint would refuse anyone else.
            if (string.IsNullOrWhiteSpace(await _tokens.GetTokenAsync())) return;
            await _apiClient.LogActivityAsync(visit);
        }
        catch
        {
            // Fire and forget: a lost page visit must never surface to the user.
        }
    }

    private static string CleanRoute(string uri)
    {
        try
        {
            var path = new Uri(uri).AbsolutePath;
            return string.IsNullOrEmpty(path) ? "/" : path;
        }
        catch
        {
            return "/";
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_initialized)
        {
            _navigationManager.LocationChanged -= OnLocationChanged;
            _lifecycle.Changed -= OnLifecycleChanged;
        }
        FlushCurrent(null);
    }
}
