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

// Config-driven API base URL (wwwroot/appsettings.json).
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5050/api";

// Web token/theme storage = browser localStorage.
builder.Services.AddScoped<ITokenStore, LocalStorageTokenStore>();
builder.Services.AddScoped<IThemeStore, LocalStorageThemeStore>();

builder.Services.AddAppCore(new AppConfig { ApiBaseUrl = apiBaseUrl, Platform = "Web" });

await builder.Build().RunAsync();
