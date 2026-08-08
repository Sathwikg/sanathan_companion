using App.Core.Auth;
using App.Core.Config;
using App.Core.DependencyInjection;
using App.Web.Auth;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// The shared RCL's Routes component is the app root.
builder.RootComponents.Add<App.UI.Shared.Routes>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Config-driven API base URL (wwwroot/appsettings.json). A relative value such
// as "/api" is resolved against the page origin — that is what the Docker deploy
// uses, where nginx serves this app and proxies /api to the API on one origin.
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "/api";
if (!Uri.IsWellFormedUriString(apiBaseUrl, UriKind.Absolute))
    apiBaseUrl = new Uri(new Uri(builder.HostEnvironment.BaseAddress), apiBaseUrl).ToString();

// Web token/theme storage = browser localStorage.
builder.Services.AddScoped<ITokenStore, LocalStorageTokenStore>();
builder.Services.AddScoped<IThemeStore, LocalStorageThemeStore>();
builder.Services.AddScoped<ILanguageStore, LocalStorageLanguageStore>();
builder.Services.AddScoped<ILocalizationCache, LocalStorageLocalizationCache>();

builder.Services.AddAppCore(new AppConfig { ApiBaseUrl = apiBaseUrl, Platform = "Web" });

await builder.Build().RunAsync();
