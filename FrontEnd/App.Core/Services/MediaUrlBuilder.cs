using App.Core.Config;

namespace App.Core.Services;

/// <summary>
/// Builds the absolute URLs that &lt;img&gt;, &lt;audio&gt; and download links use, with the
/// short-lived ticket the media endpoints ask for.
/// </summary>
/// <remarks>
/// The ticket has to be attached in the URL because those elements are fetched by the browser
/// itself, which will not add a bearer header for us. That is also the cost: the query string is
/// part of the HTTP cache key, so every rotation of the ticket re-downloads every visible image.
/// The window is six hours for exactly that reason.
/// <para>
/// <see cref="EnsureAsync"/> is awaited by pages before they render media; <see cref="Absolute"/>
/// is synchronous so it can be called straight from markup. Before the first fetch completes it
/// returns an unticketed URL, which the server accepts or refuses depending on configuration —
/// either way the page recovers on its next render rather than showing a broken image forever.
/// </para>
/// </remarks>
public class MediaUrlBuilder : IUserSessionState
{
    private readonly AppConfig _config;
    private readonly IApiClient _api;

    private string? _ticket;
    private DateTime _renewAtUtc = DateTime.MinValue;
    private Task? _inFlight;

    public MediaUrlBuilder(AppConfig config, IApiClient api)
    {
        _config = config;
        _api = api;
    }

    /// <summary>Fetches a ticket if there is not a usable one already. Safe to call repeatedly.</summary>
    public Task EnsureAsync()
    {
        if (_ticket is not null && DateTime.UtcNow < _renewAtUtc) return Task.CompletedTask;

        // One fetch at a time: a gallery renders many tiles and they all call this at once.
        return _inFlight ??= FetchAsync();
    }

    /// <summary>The absolute URL for a media route, ticket attached when one is held.</summary>
    public string Absolute(string route)
    {
        var url = _config.Absolute(route);
        if (_ticket is null) return url;

        return url + (url.Contains('?') ? '&' : '?') + "t=" + Uri.EscapeDataString(_ticket);
    }

    /// <summary>A new session gets a new ticket; the old one belonged to nobody in particular, but
    /// re-fetching costs one request and keeps the lifetime honest.</summary>
    public void Reset()
    {
        _ticket = null;
        _renewAtUtc = DateTime.MinValue;
        _inFlight = null;
    }

    private async Task FetchAsync()
    {
        try
        {
            var issued = await _api.GetMediaTicketAsync();
            if (issued is not null && !string.IsNullOrWhiteSpace(issued.Ticket))
            {
                _ticket = issued.Ticket;
                // Renew a little early rather than on the stroke: a ticket that expires between
                // building the URL and the browser fetching it is a broken image.
                _renewAtUtc = issued.ExpiresAtUtc.AddMinutes(-30);
            }
        }
        catch
        {
            // Offline, or the endpoint is not there yet. Unticketed URLs still work while the
            // server has the requirement switched off, and the next call tries again.
        }
        finally
        {
            _inFlight = null;
        }
    }
}
