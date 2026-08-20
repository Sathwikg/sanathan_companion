using App.Core.Models;
using Microsoft.JSInterop;

namespace App.Core.Services;

/// <summary>
/// Browser geolocation, via the <c>scGeo.current</c> wrapper in geo.js. The default provider:
/// correct for the web host, and the fallback if a native host does not register its own.
/// </summary>
public class JsGeolocationProvider : IGeolocationProvider
{
    private readonly IJSRuntime _js;

    public JsGeolocationProvider(IJSRuntime js) => _js = js;

    public async Task<GeoPosition> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // geo.js resolves rather than rejects, so a refusal arrives as a populated Error.
            var fix = await _js.InvokeAsync<GeoPosition>("scGeo.current", cancellationToken);
            if (fix is null) return new GeoPosition { Error = "Could not get your location." };
            if (fix.Error is not null) return fix;

            // Coarsened here, at the edge, so the precise fix never reaches the rest of the app.
            fix.Latitude = GeoPrecision.Round(fix.Latitude);
            fix.Longitude = GeoPrecision.Round(fix.Longitude);
            return fix;
        }
        catch (JSException ex)
        {
            return new GeoPosition { Error = ex.Message };
        }
    }
}
