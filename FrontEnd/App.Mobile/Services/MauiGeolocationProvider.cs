using App.Core.Models;
using App.Core.Services;

namespace App.Mobile.Services;

/// <summary>
/// Device geolocation through MAUI, replacing the browser call the web host uses.
/// </summary>
/// <remarks>
/// Inside a Blazor WebView, <c>navigator.geolocation</c> is denied on both platforms unless the
/// native host answers a permission callback — on Android that means overriding the WebChromeClient
/// that MAUI already installs for the file picker, and on iOS it needs the usage strings anyway.
/// Going through <see cref="Geolocation"/> raises the same system prompt the rest of the OS uses,
/// on both platforms, without touching the WebView's own plumbing.
/// </remarks>
public class MauiGeolocationProvider : IGeolocationProvider
{
    /// <summary>
    /// Long enough for a cold GPS fix indoors, short enough that a seeker does not think the app
    /// has hung. Matches the 10s the browser wrapper asks for.
    /// </summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public async Task<GeoPosition> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // The cached fix first: it is free and, for a Panchangam calculated to the nearest
            // sunrise, a few hundred metres of staleness changes nothing.
            var location = await Geolocation.Default.GetLastKnownLocationAsync()
                           ?? await Geolocation.Default.GetLocationAsync(
                                  new GeolocationRequest(GeolocationAccuracy.Medium, Timeout),
                                  cancellationToken);

            if (location is null)
                return new GeoPosition { Error = "Your location is currently unavailable." };

            return new GeoPosition
            {
                // Coarsened for the same reason as the web provider; see GeoPrecision.
                Latitude = GeoPrecision.Round(location.Latitude),
                Longitude = GeoPrecision.Round(location.Longitude),
                Accuracy = location.Accuracy ?? 0
            };
        }
        catch (PermissionException)
        {
            return new GeoPosition { Error = "Location permission was denied." };
        }
        catch (FeatureNotEnabledException)
        {
            return new GeoPosition { Error = "Location services are switched off on this device." };
        }
        catch (FeatureNotSupportedException)
        {
            return new GeoPosition { Error = "This device cannot report its location." };
        }
        catch (OperationCanceledException)
        {
            return new GeoPosition { Error = "Getting your location timed out." };
        }
        catch (Exception ex)
        {
            return new GeoPosition { Error = "Could not get your location. " + ex.Message };
        }
    }
}
