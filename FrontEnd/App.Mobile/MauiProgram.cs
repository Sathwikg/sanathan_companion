using App.Core.Auth;
using App.Core.DependencyInjection;
using App.Core.Services;
using App.Mobile.Auth;
using App.Mobile.Configuration;
using App.Mobile.Services;
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
        builder.Services.AddScoped<ILanguageStore, PreferencesLanguageStore>();
        // Lets the app start up translated with no network — important on mobile, where the API
        // lives on Render and a cold start (or no signal) would otherwise mean English.
        builder.Services.AddScoped<ILocalizationCache, FileLocalizationCache>();

        // Every setting the app has — including where the API lives — comes from the one file at
        // Resources/Raw/appsettings.json. Nothing else in this project should hardcode a URL.
        builder.Services.AddAppCore(MobileSettings.Load());

        // Registered after AddAppCore so it replaces the browser-backed default. A WebView's
        // navigator.geolocation is denied on both platforms; MAUI's Geolocation raises the real
        // system prompt instead. See MauiGeolocationProvider.
        builder.Services.AddScoped<IGeolocationProvider, MauiGeolocationProvider>();

        return builder.Build();
    }
}
