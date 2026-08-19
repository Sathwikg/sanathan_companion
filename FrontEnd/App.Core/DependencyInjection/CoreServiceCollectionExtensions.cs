using App.Core.Auth;
using App.Core.Config;
using App.Core.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace App.Core.DependencyInjection;

public static class CoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the shared client services. Each host must additionally register an
    /// <see cref="ITokenStore"/> and <see cref="IThemeStore"/> for its platform.
    /// </summary>
    public static IServiceCollection AddAppCore(this IServiceCollection services, AppConfig config)
    {
        services.AddSingleton(config);

        services.AddScoped<BearerTokenHandler>();
        // SINGLETON for the same reason as LanguageContext below: the handler that raises this
        // lives in HttpClientFactory's scope, not the component's.
        services.AddSingleton<SessionExpiredNotifier>();
        services.AddScoped<SessionExpiryHandler>();
        // SINGLETON, not scoped: HttpClientFactory resolves message handlers from its own scope, so
        // a scoped context would give LanguageHeaderHandler a different instance than the one
        // LocalizationState writes to — and every request would ship a stale language.
        services.AddSingleton<LanguageContext>();
        services.AddScoped<LanguageHeaderHandler>();
        services.AddScoped<JwtAuthenticationStateProvider>();
        services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthenticationStateProvider>());
        services.AddAuthorizationCore();

        services.AddScoped<IAuthService, AuthService>();
        // Browser geolocation by default. A native host registers its own AFTER calling this, so
        // the last registration wins and the WebView's blocked navigator.geolocation is bypassed.
        services.AddScoped<IGeolocationProvider, JsGeolocationProvider>();
        // Same pattern: a browser cannot schedule a notification that outlives its tab, so the
        // default does nothing and the MAUI host replaces it.
        services.AddScoped<IReminderScheduler, NoOpReminderScheduler>();
        // A browser already throttles hidden tabs; a native host replaces this so shared
        // components can stop their timers when the app is backgrounded.
        services.AddSingleton<IAppLifecycle, AlwaysActiveLifecycle>();
        services.AddScoped<MenuRefreshService>();
        services.AddScoped<ConfirmService>();
        services.AddScoped<ToastService>();
        services.AddScoped<NotificationCenterState>();
        // Per-user caches. Registered twice so the IUserSessionState reset (on sign-in/out)
        // acts on the very same instances the components inject.
        services.AddScoped<FavoritesState>();
        services.AddScoped<RegionState>();
        services.AddScoped<LocalizationState>();
        services.AddScoped<IUserSessionState>(sp => sp.GetRequiredService<FavoritesState>());
        services.AddScoped<IUserSessionState>(sp => sp.GetRequiredService<RegionState>());
        // Also a session state: signing out must cancel the scheduled reminders, or the previous
        // seeker's 06:00 sadhana nudge keeps firing on the next account's phone.
        services.AddScoped<IUserSessionState>(sp => sp.GetRequiredService<NotificationCenterState>());

        services.AddHttpClient<IApiClient, ApiClient>(client =>
            {
                // Absolute by contract: each host resolves a relative setting such as "/api"
                // against its own origin before it ever reaches AppConfig.
                client.BaseAddress = new Uri(config.NormalisedApiBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(config.HttpTimeoutSeconds);
            })
            // Order matters: the expiry handler must sit OUTSIDE the bearer handler so it sees the
            // Authorization header the inner handler added, which is how it tells an expired
            // session apart from a failed sign-in.
            .AddHttpMessageHandler<SessionExpiryHandler>()
            .AddHttpMessageHandler<BearerTokenHandler>()
            // Stamps X-App-Language so the server translates database text for this user.
            .AddHttpMessageHandler<LanguageHeaderHandler>();

        return services;
    }
}
