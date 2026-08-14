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
            return await _js.InvokeAsync<GeoPosition>("scGeo.current", cancellationToken)
                   ?? new GeoPosition { Error = "Could not get your location." };
        }
        catch (JSException ex)
        {
            return new GeoPosition { Error = ex.Message };
        }
    }
}
