namespace App.Core.Config;

/// <summary>The hosts the shared UI can run in. The value travels to the API, so it is not free text.</summary>
public static class PlatformNames
{
    /// <summary>Blazor WebAssembly in a browser.</summary>
    public const string Web = "Web";

    /// <summary>.NET MAUI Blazor Hybrid — Android, iOS, Mac Catalyst and Windows.</summary>
    /// <remarks>
    /// Matched case-insensitively by <c>MenuModuleService.GetMenuAsync</c> to pick
    /// <c>ModuleRoleMapping.MobileEnabled</c> over <c>WebEnabled</c>. Renaming it here silently
    /// demotes every non-Admin user to the web access rights, so keep the two in step.
    /// </remarks>
    public const string Mobile = "Mobile";
}

/// <summary>
/// Everything a host configures about itself, in one object.
/// </summary>
/// <remarks>
/// Registered as a singleton by <c>AddAppCore</c> and injectable anywhere, so a component never
/// has to know <em>how</em> its host was configured — only what the answer is. Each host fills it
/// from its own single source: <c>wwwroot/appsettings.json</c> for the web, and
/// <c>Resources/Raw/appsettings.json</c> for mobile.
/// </remarks>
public class AppConfig
{
    /// <summary>
    /// Root of the REST API, with or without a trailing slash. May be relative (<c>/api</c>) on the
    /// web, where the SPA and the API share an origin behind nginx; must be absolute on mobile,
    /// which has no origin to be relative to.
    /// </summary>
    public string ApiBaseUrl { get; set; } = "http://localhost:7050/api";

    /// <summary>Which host this is running in — see <see cref="PlatformNames"/>. Drives per-platform menu access rights.</summary>
    public string Platform { get; set; } = PlatformNames.Web;

    /// <summary>Shown in the shell and on the login screen when no translation overrides it.</summary>
    public string AppName { get; set; } = "Sanathan Companion";

    /// <summary>Free-text environment label ("Development", "Production") surfaced in the profile screen's About row.</summary>
    public string Environment { get; set; } = "Production";

    /// <summary>Marketing version shown to the user. Mobile overrides it from the package metadata.</summary>
    public string AppVersion { get; set; } = "1.0";

    /// <summary>
    /// Ceiling on a single API call. Generous by default because the API's free-tier host cold-starts,
    /// and a phone on a weak signal is the case that actually needs the headroom.
    /// </summary>
    public int HttpTimeoutSeconds { get; set; } = 100;

    /// <summary>True when the shared UI should render its phone shell (bottom navigation) instead of the desktop rail.</summary>
    public bool IsMobile => string.Equals(Platform, PlatformNames.Mobile, StringComparison.OrdinalIgnoreCase);

    /// <summary>The base URL normalised for <see cref="HttpClient.BaseAddress"/>, which drops the last segment without it.</summary>
    public string NormalisedApiBaseUrl => ApiBaseUrl.EndsWith('/') ? ApiBaseUrl : ApiBaseUrl + "/";

    /// <summary>
    /// Turns a relative route into the absolute URL an <c>&lt;img src&gt;</c>, <c>&lt;audio src&gt;</c>
    /// or download link needs — those are fetched by the browser against the page's own origin,
    /// which is the app, not the API.
    /// </summary>
    /// <example><c>Config.Absolute(ApiRoutes.Media.DeityImage(id))</c></example>
    public string Absolute(string relativeRoute) => NormalisedApiBaseUrl + relativeRoute.TrimStart('/');
}
