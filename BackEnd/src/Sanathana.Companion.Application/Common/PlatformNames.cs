namespace Sanathana.Companion.Application.Common;

/// <summary>
/// The client hosts that ask for a menu, as supplied in <c>GET /api/menumodules/menu?platform=</c>.
/// </summary>
/// <remarks>
/// Client-supplied and unverified — it selects which column of the access matrix to read, not what
/// the caller is entitled to. Per-endpoint <c>[Authorize]</c> remains the real gate.
/// The frontend's <c>App.Core.Config.PlatformNames</c> must spell these the same way.
/// </remarks>
public static class PlatformNames
{
    /// <summary>Blazor WebAssembly in a browser. The default when the caller says nothing.</summary>
    public const string Web = "Web";

    /// <summary>.NET MAUI Blazor Hybrid — Android, iOS, Mac Catalyst and Windows.</summary>
    public const string Mobile = "Mobile";
}
