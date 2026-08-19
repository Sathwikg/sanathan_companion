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
        // SINGLETON, not scoped. The store caches the token in memory, and IHttpClientFactory
        // resolves delegating handlers from its OWN scope — so a scoped registration would give
        // BearerTokenHandler a different instance than AuthService: the login POST would latch
        // "no token" on the handler's copy and every request after sign-in would go out
        // unauthenticated, while sign-out would leave the previous seeker's JWT in the handler's
        // cache. The same trap is already documented for LanguageContext and SessionExpiredNotifier.
        builder.Services.AddSingleton<ITokenStore, SecureStorageTokenStore>();
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

        // Same override pattern. The API models what a seeker wants to be reminded about but has
        // no way to deliver it, so the phone schedules the reminders locally.
        builder.Services.AddScoped<IReminderScheduler, MauiReminderScheduler>();

        // Singleton: App.xaml.cs pushes window Activated/Deactivated into it, and every component
        // that idles while backgrounded reads the same instance.
        builder.Services.AddSingleton<IAppLifecycle, MauiAppLifecycle>();

        return builder.Build();
    }
}
