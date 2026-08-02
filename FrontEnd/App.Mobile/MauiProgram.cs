using App.Core.Auth;
using App.Core.Config;
using App.Core.DependencyInjection;
using App.Mobile.Auth;
using Microsoft.Extensions.Logging;

namespace App.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        // Platform storage
        builder.Services.AddScoped<ITokenStore, SecureStorageTokenStore>();
        builder.Services.AddScoped<IThemeStore, PreferencesThemeStore>();

        // Where the API lives. Debug keeps the loopback targets — the Android
        // emulator reaches the host through 10.0.2.2, everything else through
        // localhost — so `dotnet run` against a local API still works. Release
        // points at the deployment on Render.
        //
        // Unlike App.Web, which reads wwwroot/appsettings.json at startup and
        // can be repointed by rewriting that file, this is compiled in: a
        // packaged app has no config file to edit, so changing the target means
        // a rebuild. Keep it in sync with `name` in render.yaml — Render appends
        // a suffix if the service name was already taken, so confirm the host on
        // the service page rather than assuming it.
#if DEBUG
    #if ANDROID
        const string apiBaseUrl = "http://10.0.2.2:7050/api";
    #else
        const string apiBaseUrl = "http://localhost:7050/api";
    #endif
#else
        const string apiBaseUrl = "https://sanathana-companion.onrender.com/api";
#endif
        builder.Services.AddAppCore(new AppConfig { ApiBaseUrl = apiBaseUrl, Platform = "Mobile" });

        return builder.Build();
    }
}
