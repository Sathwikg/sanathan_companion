namespace App.Core.Services;

/// <summary>
/// The language and UI route to stamp on every outgoing API call.
/// </summary>
/// <remarks>
/// A tiny holder rather than reading <see cref="LocalizationState"/> directly, and that is
/// deliberate: <c>LocalizationState</c> depends on <c>IApiClient</c>, so a delegating handler in
/// that same client's pipeline could not inject it without creating a DI cycle. Both sides write
/// to and read from this instead.
/// </remarks>
public class LanguageContext
{
    /// <summary>Selected language code, e.g. "te". Null until the preference has been read.</summary>
    public string? Code { get; set; }

    /// <summary>Current UI route, so the server can honour the per-form opt-out.</summary>
    public string? Route { get; set; }
}
