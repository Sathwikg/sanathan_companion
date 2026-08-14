using App.Core.Models;

namespace App.Core.Services;

/// <summary>Where the app gets "where am I" from. One implementation per host.</summary>
/// <remarks>
/// The web host asks the browser. The MAUI host must not: a WebView's
/// <c>navigator.geolocation</c> is gated behind a native permission callback that neither
/// platform's Blazor WebView wires up, so the call is simply denied on a device. The mobile host
/// registers a provider backed by MAUI's own Geolocation API instead, which raises the real
/// system permission prompt and works identically on Android and iOS.
/// <para>
/// Never throws for a routine refusal — a denied prompt or a location fix that never arrives is a
/// normal outcome, reported through <see cref="GeoPosition.Error"/> so the page can show it
/// beside the button that asked.
/// </para>
/// </remarks>
public interface IGeolocationProvider
{
    Task<GeoPosition> GetCurrentAsync(CancellationToken cancellationToken = default);
}
