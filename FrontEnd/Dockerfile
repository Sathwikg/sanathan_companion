# syntax=docker/dockerfile:1
# Sanathana Companion web host: Blazor WebAssembly published to static files and
# served by nginx, which also reverse-proxies /api to the API container.
# Build context is the FrontEnd/ folder.

# ---- build -----------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Only App.Web and its two dependencies are built. App.Mobile is excluded on
# purpose — it needs the MAUI workloads and produces nothing for the web host.
COPY App.Core/App.Core.csproj           App.Core/
COPY App.UI.Shared/App.UI.Shared.csproj App.UI.Shared/
COPY App.Web/App.Web.csproj             App.Web/
RUN dotnet restore App.Web/App.Web.csproj

# If a future change turns on AOT or runtime relinking, this build will ask for
# the wasm-tools workload — uncomment the next line at that point.
# RUN dotnet workload install wasm-tools

COPY App.Core/      App.Core/
COPY App.UI.Shared/ App.UI.Shared/
COPY App.Web/       App.Web/
RUN dotnet publish App.Web/App.Web.csproj -c Release -o /app/publish --no-restore

# ---- runtime ---------------------------------------------------------------
FROM nginx:1.27-alpine AS final

COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf

# Rewrites wwwroot/appsettings.json from $API_BASE_URL on every container start,
# so the published SPA can be pointed at a different API without a rebuild.
COPY docker/20-api-base-url.sh /docker-entrypoint.d/20-api-base-url.sh
RUN chmod +x /docker-entrypoint.d/20-api-base-url.sh

EXPOSE 80
