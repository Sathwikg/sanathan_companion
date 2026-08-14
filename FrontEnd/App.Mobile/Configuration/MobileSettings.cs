using System.Text.Json;
using App.Core.Config;

namespace App.Mobile.Configuration;

/// <summary>
/// Reads <c>Resources/Raw/appsettings.json</c> — the single place the mobile app is configured —
/// and turns it into the <see cref="AppConfig"/> the shared UI consumes.
/// </summary>
/// <remarks>
/// A packaged app has no editable config file on disk, so this is baked into the bundle: changing
/// the API target still means a rebuild. What it buys is that the target lives in one obvious,
/// reviewable JSON file instead of being spread across <c>#if</c> blocks in start-up code, and that
/// the same file is what iOS and Android both read.
/// <para>
/// Every field falls back to a compiled default, so a missing or malformed asset degrades to a
/// working app rather than a crash on the splash screen.
/// </para>
/// </remarks>
public static class MobileSettings
{
    private const string AssetFileName = "appsettings.json";

    public static AppConfig Load()
    {
        var file = ReadAsset();

        return new AppConfig
        {
            ApiBaseUrl = ResolveApiBaseUrl(file),
            Platform = PlatformNames.Mobile,
            AppName = Coalesce(file?.AppName, "Sanathan Companion"),
            Environment = Coalesce(file?.Environment, DefaultEnvironment),
            AppVersion = AppInfo.Current.VersionString,
            HttpTimeoutSeconds = file?.HttpTimeoutSeconds is int seconds && seconds > 0 ? seconds : 100
        };
    }

    /// <summary>
    /// Debug builds talk to a local API; Release builds talk to the deployment. The Android
    /// emulator is the exception that needs its own host: 127.0.0.1 inside the emulator is the
    /// emulator itself, and the development machine is 10.0.2.2.
    /// </summary>
    private static string ResolveApiBaseUrl(SettingsFile? file)
    {
#if DEBUG
    #if ANDROID
        return Coalesce(file?.ApiBaseUrlDebugAndroid, "http://10.0.2.2:7050/api");
    #else
        return Coalesce(file?.ApiBaseUrlDebug, "http://localhost:7050/api");
    #endif
#else
        return Coalesce(file?.ApiBaseUrl, "https://sanathana-companion.onrender.com/api");
#endif
    }

    private static string DefaultEnvironment =>
#if DEBUG
        "Development";
#else
        "Production";
#endif

    /// <summary>
    /// Blocking on purpose: MAUI's start-up path is synchronous, and the app cannot resolve a single
    /// service until it knows where the API is. The read is a bundle lookup, not I/O over a network,
    /// and <see cref="Task.Run(Func{Task})"/> keeps it off whatever synchronisation context the
    /// platform installed so it cannot deadlock.
    /// </summary>
    private static SettingsFile? ReadAsset()
    {
        try
        {
            return Task.Run(async () =>
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync(AssetFileName);
                return await JsonSerializer.DeserializeAsync<SettingsFile>(stream, JsonOptions);
            }).GetAwaiter().GetResult();
        }
        catch
        {
            // Missing or malformed: the compiled defaults below still produce a working app.
            return null;
        }
    }

    private static string Coalesce(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>Shape of appsettings.json. Keys prefixed with "//" in the file are documentation and bind to nothing.</summary>
    private sealed class SettingsFile
    {
        public string? AppName { get; set; }
        public string? Environment { get; set; }
        public string? ApiBaseUrl { get; set; }
        public string? ApiBaseUrlDebug { get; set; }
        public string? ApiBaseUrlDebugAndroid { get; set; }
        public int? HttpTimeoutSeconds { get; set; }
    }
}
