using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Sanathana.Companion.Application.Common.Translation;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Api.Filters;

/// <summary>
/// The single choke point where database text becomes the caller's language.
/// </summary>
/// <remarks>
/// <para>
/// Runs before serialisation, so it sees typed DTOs and their <see cref="TranslatableAttribute"/>
/// markings. Middleware could not do this — it only sees bytes, and would have to translate every
/// string blindly, including people's names.
/// </para>
/// <para>
/// Because it is global, annotating a DTO property is the only work needed to localise a new form.
/// </para>
/// </remarks>
public sealed class TranslationResultFilter : IAsyncResultFilter
{
    /// <summary>Client's chosen language. Preferred over Accept-Language, which is the browser's.</summary>
    public const string LanguageHeader = "X-App-Language";

    /// <summary>UI route, so the Forms tab opt-out can be honoured server-side.</summary>
    public const string RouteHeader = "X-App-Route";

    /// <summary>Set to "none" by edit screens that must receive untranslated text (see below).</summary>
    public const string OptOutHeader = "X-Translate";

    private readonly ITranslationCatalog _catalog;
    private readonly ITranslationMissLog _misses;
    private readonly ILogger<TranslationResultFilter> _logger;

    public TranslationResultFilter(
        ITranslationCatalog catalog, ITranslationMissLog misses, ILogger<TranslationResultFilter> logger)
    {
        _catalog = catalog;
        _misses = misses;
        _logger = logger;
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        // Always advertise the variance, even for English: otherwise a proxy or browser cache can
        // hand a Telugu payload to an English client.
        context.HttpContext.Response.Headers.Append("Vary", LanguageHeader);

        try
        {
            await TranslateAsync(context);
        }
        catch (Exception ex)
        {
            // A translation fault must never turn a good 200 into a 500 — fall back to English.
            _logger.LogError(ex, "Response translation failed; returning untranslated text.");
        }

        await next();
    }

    private async Task TranslateAsync(ResultExecutingContext context)
    {
        var request = context.HttpContext.Request;

        // Only GETs. A write endpoint echoes back what the client sent, and translating that
        // would corrupt round-trips.
        if (!HttpMethods.IsGet(request.Method)) return;

        if (context.Result is not ObjectResult { Value: { } value }) return;

        // Explicit opt-out for screens that load data in order to save it again.
        if (string.Equals(request.Headers[OptOutHeader], "none", StringComparison.OrdinalIgnoreCase)) return;

        var code = ResolveLanguage(request);
        if (code is null) return;

        var snapshot = await _catalog.GetAsync(code, context.HttpContext.RequestAborted);
        if (snapshot is null || snapshot.IsEmpty) return;

        // An admin can opt a whole form out of a language on the Forms tab.
        var route = request.Headers[RouteHeader].ToString();
        if (!snapshot.IsRouteTranslated(route)) return;

        new ObjectGraphTranslator(snapshot, _misses).Walk(value);
    }

    /// <summary>Explicit header, then query string, then the browser's own preference.</summary>
    private static string? ResolveLanguage(HttpRequest request)
    {
        var header = request.Headers[LanguageHeader].ToString();
        if (!string.IsNullOrWhiteSpace(header)) return header.Trim();

        if (request.Query.TryGetValue("lang", out var q) && !string.IsNullOrWhiteSpace(q))
            return q.ToString().Trim();

        var accept = request.Headers.AcceptLanguage.ToString();
        if (string.IsNullOrWhiteSpace(accept)) return null;

        // "te-IN,te;q=0.9,en;q=0.8" -> "te"
        var first = accept.Split(',')[0].Split(';')[0].Trim();
        if (first.Length == 0) return null;
        var dash = first.IndexOf('-');
        return dash > 0 ? first[..dash] : first;
    }
}
