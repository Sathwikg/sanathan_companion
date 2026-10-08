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
        // A crash on a native thread ends the process before any HTTP call could finish, so it is
        // parked in a file and reported by ClientErrorReporter on the next start.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) FileCrashStore.Save(ex);
        };

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

        // Same override pattern. A WebView's navigator.share is not wired to the system chooser, so
        // the rendered picture goes out through MAUI's Share. See MauiImageShare.
        builder.Services.AddScoped<IImageShare, MauiImageShare>();

        // Same override pattern. The API models what a seeker wants to be reminded about but has
        // no way to deliver it, so the phone schedules the reminders locally.
        builder.Services.AddScoped<IReminderScheduler, MauiReminderScheduler>();

        // Singleton: App.xaml.cs pushes window Activated/Deactivated into it, and every component
        // that idles while backgrounded reads the same instance.
        builder.Services.AddSingleton<IAppLifecycle, MauiAppLifecycle>();

        // Replaces the browser's no-op so the crash saved above reaches the server's error log.
        builder.Services.AddSingleton<ICrashStore, FileCrashStore>();

        // Sign in with Google. Android gets the native Credential Manager picker; every other head
        // gets "none": iOS because offering Google there obliges the app to offer Sign in with Apple
        // too (App Store guideline 4.8), Windows and Mac because they are development heads.
#if ANDROID
        // global:: because inside namespace App.Mobile the bare name App is the App class.
        builder.Services.AddScoped<IGoogleSignIn, global::App.Mobile.Platforms.Android.Services.AndroidGoogleSignIn>();
#else
        builder.Services.AddScoped<IGoogleSignIn, UnavailableGoogleSignIn>();
#endif

        return builder.Build();
    }
}
