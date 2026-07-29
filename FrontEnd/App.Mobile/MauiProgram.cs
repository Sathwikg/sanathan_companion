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

        // Android emulator reaches the host loopback via 10.0.2.2; Windows uses localhost.
#if ANDROID
        const string apiBaseUrl = "http://10.0.2.2:7050/api";
#else
        const string apiBaseUrl = "http://localhost:7050/api";
#endif
        builder.Services.AddAppCore(new AppConfig { ApiBaseUrl = apiBaseUrl, Platform = "Mobile" });

        return builder.Build();
    }
}
