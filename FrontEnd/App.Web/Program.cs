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

// Almost always "Web". Set to "Mobile" and this host renders the phone shell — bottom navigation,
// top notifications, the mobile skin — and asks the API for the mobile menu, which is how the MAUI
// app's UI is reviewed in a desktop browser's device emulation without a device or an emulator.
// Not for the service users log in to: it also switches every non-Admin user to the Mobile column
// of the access matrix.
//
// Three ways in; on development the second is the one to reach for locally:
//
//   Platform in wwwroot/appsettings.json — on development a working-tree edit to a tracked file
//   that must be reverted before committing, because forgetting it ships the access-matrix
//   switch. On THIS branch (development_mobileview) it is committed as Mobile: the branch exists
//   to be the phone preview, so a plain `dotnet run` renders the phone shell, and
//   wwwroot/phoneFrame.js frames it as a handset in a desktop-sized window.
//
//   The `mobile` launch profile — `dotnet run --project FrontEnd/App.Web --launch-profile mobile`
//   serves the same build on :7002 with ASPNETCORE_ENVIRONMENT=Mobile, so the web shell on :7001
//   and the phone shell on :7002 run side by side and nothing in the tree changes.
//
//   PLATFORM=Mobile on a deployed container — deploy/render/entrypoint.sh writes the variable into
//   the appsettings.json it generates at start, which is how the development_mobileview branch
//   hosts the phone shell as its own Render service without a tracked file changing.
//
// The origin is what distinguishes the two, because nothing else can. Both heads are ONE build
// served twice, so they read the same appsettings.json; and the WebAssembly host neither fetches
// appsettings.{Environment}.json nor honours the `Blazor-Environment` header the dev server sends
// on _framework/blazor.boot.json — both verified here, the client reports "Development" either
// way. BaseAddress is the one per-instance fact the client can actually observe.
var mobileOrigins = builder.Configuration.GetSection("MobilePreviewOrigins").Get<string[]>()
                    ?? Array.Empty<string>();

var origin = builder.HostEnvironment.BaseAddress.TrimEnd('/');
var isMobilePreviewOrigin = mobileOrigins.Any(
    o => string.Equals(o?.TrimEnd('/'), origin, StringComparison.OrdinalIgnoreCase));

var platform = string.Equals(builder.Configuration["Platform"], PlatformNames.Mobile, StringComparison.OrdinalIgnoreCase)
            || isMobilePreviewOrigin
    ? PlatformNames.Mobile
    : PlatformNames.Web;

builder.Services.AddAppCore(new AppConfig
{
    ApiBaseUrl = apiBaseUrl,
    Platform = platform,
    AppName = builder.Configuration["AppName"] ?? "Sanathan Companion",
    Environment = builder.Configuration["Environment"] ?? "Production"
});

await builder.Build().RunAsync();
