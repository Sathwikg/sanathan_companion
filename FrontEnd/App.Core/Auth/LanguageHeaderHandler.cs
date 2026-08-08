using App.Core.Services;

namespace App.Core.Auth;

/// <summary>
/// Stamps the selected language (and current route) on every API request, so the server can
/// translate database text. One registration covers web and mobile alike.
/// </summary>
public class LanguageHeaderHandler : DelegatingHandler
{
    /// <summary>Must match <c>TranslationResultFilter.LanguageHeader</c> on the server.</summary>
    private const string LanguageHeader = "X-App-Language";
    private const string RouteHeader = "X-App-Route";

    private readonly LanguageContext _context;
    private readonly ILanguageStore _store;

    public LanguageHeaderHandler(LanguageContext context, ILanguageStore store)
    {
        _context = context;
        _store = store;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var code = _context.Code;

        // A page can fire its first request before LocalizationState has finished loading, so fall
        // back to the stored preference rather than letting that race show English. Deliberately
        // NOT written back to the context: LocalizationState is the single writer, and caching a
        // startup value here would pin the header to the old language after a switch.
        if (string.IsNullOrWhiteSpace(code))
        {
            try { code = await _store.GetLanguageAsync(); }
            catch { /* storage unavailable — send untranslated rather than fail the request */ }
        }

        if (!string.IsNullOrWhiteSpace(code))
            request.Headers.TryAddWithoutValidation(LanguageHeader, code);

        if (!string.IsNullOrWhiteSpace(_context.Route))
            request.Headers.TryAddWithoutValidation(RouteHeader, _context.Route);

        return await base.SendAsync(request, cancellationToken);
    }
}
