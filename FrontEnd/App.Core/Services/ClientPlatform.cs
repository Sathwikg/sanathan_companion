namespace App.Core.Services;

/// <summary>
/// The device family this build is running on, as the audit log records it.
/// </summary>
/// <remarks>
/// Distinct from <see cref="Config.AppConfig.Platform"/>, which is the shell (web or mobile) and can
/// be switched to "Mobile" in a desktop browser for previewing. This is what the code is actually
/// running on: the MAUI heads report Android or iOS, WebAssembly reports Web.
/// </remarks>
public static class ClientPlatform
{
    /// <summary>Must match <c>HttpAuditRequestContext.PlatformHeader</c> on the server.</summary>
    public const string Header = "X-Platform";

    public static string Name { get; } =
        OperatingSystem.IsAndroid() ? "Android" :
        OperatingSystem.IsIOS() ? "iOS" :
        "Web";

    public static bool IsNative => Name != "Web";

    /// <summary>The error-log source a crash on this device is filed under.</summary>
    public static string ErrorSource => IsNative ? "FrontendMobile" : "FrontendWeb";
}
