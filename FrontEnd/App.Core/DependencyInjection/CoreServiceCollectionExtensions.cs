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
        services.AddScoped<JwtAuthenticationStateProvider>();
        services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthenticationStateProvider>());
        services.AddAuthorizationCore();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<MenuRefreshService>();
        services.AddScoped<ConfirmService>();
        services.AddScoped<ToastService>();
        // Per-user caches. Registered twice so the IUserSessionState reset (on sign-in/out)
        // acts on the very same instances the components inject.
        services.AddScoped<FavoritesState>();
        services.AddScoped<RegionState>();
        services.AddScoped<IUserSessionState>(sp => sp.GetRequiredService<FavoritesState>());
        services.AddScoped<IUserSessionState>(sp => sp.GetRequiredService<RegionState>());

        var baseUrl = config.ApiBaseUrl.EndsWith('/') ? config.ApiBaseUrl : config.ApiBaseUrl + "/";
        services.AddHttpClient<IApiClient, ApiClient>(client => client.BaseAddress = new Uri(baseUrl))
            .AddHttpMessageHandler<BearerTokenHandler>();

        return services;
    }
}
