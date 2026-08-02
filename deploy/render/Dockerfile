# syntax=docker/dockerfile:1
# Sanathana Companion — single-image build for Render.
#
# Render has no docker-compose support: one service is one image. This file
# folds the two compose services into one container while keeping the same
# shape, so the deployed topology still matches docker-compose.yml:
#
#   Render edge (TLS) ──► nginx :$PORT ─┬─ /      static Blazor WebAssembly
#                                       └─ /api/ ──► 127.0.0.1:8080 (Kestrel)
#
# Kestrel binds to loopback only, so the API is reachable exclusively through
# the /api proxy — the same guarantee compose gets from `expose` + a private
# network. And because the SPA and the API still share one origin, the browser
# never issues a cross-origin request and Cors:AllowedOrigins stays empty.
#
# BUILD CONTEXT IS THE REPOSITORY ROOT — both BackEnd/ and FrontEnd/ are needed.
# In render.yaml that is `dockerContext: .` with `dockerfilePath` pointing here.

# ---- build: API ------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
WORKDIR /src

# Restore against the csproj files alone so the NuGet layer survives source
# edits. Sanathana.Companion.Modules is in the solution but nothing references
# it, so the Api csproj restore covers everything that actually ships.
COPY BackEnd/Directory.Build.props ./
COPY BackEnd/src/Sanathana.Companion.Domain/Sanathana.Companion.Domain.csproj                 src/Sanathana.Companion.Domain/
COPY BackEnd/src/Sanathana.Companion.Application/Sanathana.Companion.Application.csproj       src/Sanathana.Companion.Application/
COPY BackEnd/src/Sanathana.Companion.Infrastructure/Sanathana.Companion.Infrastructure.csproj src/Sanathana.Companion.Infrastructure/
COPY BackEnd/src/Sanathana.Companion.Api/Sanathana.Companion.Api.csproj                       src/Sanathana.Companion.Api/
RUN dotnet restore src/Sanathana.Companion.Api/Sanathana.Companion.Api.csproj

COPY BackEnd/src/ src/
RUN dotnet publish src/Sanathana.Companion.Api/Sanathana.Companion.Api.csproj \
        -c Release \
        -o /app/api \
        --no-restore \
        /p:UseAppHost=false

# ---- build: Blazor WebAssembly --------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS web-build
WORKDIR /src

# Only App.Web and its two dependencies are built. App.Mobile is excluded on
# purpose — it needs the MAUI workloads and produces nothing for the web host.
COPY FrontEnd/App.Core/App.Core.csproj           App.Core/
COPY FrontEnd/App.UI.Shared/App.UI.Shared.csproj App.UI.Shared/
COPY FrontEnd/App.Web/App.Web.csproj             App.Web/
RUN dotnet restore App.Web/App.Web.csproj

COPY FrontEnd/App.Core/      App.Core/
COPY FrontEnd/App.UI.Shared/ App.UI.Shared/
COPY FrontEnd/App.Web/       App.Web/
RUN dotnet publish App.Web/App.Web.csproj -c Release -o /app/web --no-restore

# ---- runtime ---------------------------------------------------------------
# The ASP.NET runtime image is the base and nginx is added on top, rather than
# the reverse: pulling the .NET runtime into an nginx image means fetching and
# verifying it by hand, while apt gives us a maintained nginx in one line.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# gettext-base provides envsubst, used at startup to inject Render's $PORT into
# the nginx config (nginx cannot read environment variables by itself).
# curl lets you probe Kestrel directly, bypassing nginx, from a Render shell —
# note that shell access starts at the paid plans, so on free it is dead weight
# kept only so the image is ready if you upgrade. Render runs its health check
# from outside the container, so nothing here depends on curl.
RUN apt-get update \
 && apt-get install -y --no-install-recommends nginx gettext-base curl \
 && rm -rf /var/lib/apt/lists/* \
 # Debian ships a default site that also gets included from nginx.conf; drop it
 # so ours is the only server block.
 && rm -f /etc/nginx/sites-enabled/default

COPY --from=api-build /app/api            /app/api
COPY --from=web-build /app/web/wwwroot    /usr/share/nginx/html
COPY deploy/render/nginx.conf.template    /etc/nginx/templates/default.conf.template
COPY deploy/render/entrypoint.sh          /usr/local/bin/entrypoint.sh
RUN chmod +x /usr/local/bin/entrypoint.sh

# No inotify. WebApplication.CreateBuilder registers appsettings.json with
# reloadOnChange:true, which opens an inotify instance before a single line of
# our own code runs. The host kernel caps fs.inotify.max_user_instances at 128
# and that budget is shared with every other tenant on the machine, so when it
# is exhausted the watcher cannot be created and the process aborts on the
# first statement of Program.cs. A sysctl is not ours to raise here.
#
# Nothing is lost by switching it off: configuration arrives as environment
# variables and the container filesystem is immutable, so there is no edit for
# a watcher to observe.
#
#   reloadConfigOnChange=false  removes the config watcher, the one the crash
#                               stack actually named.
#   USE_POLLING_FILE_WATCHER    is the backstop — should any other component
#                               ever ask for a watcher, it degrades to polling
#                               instead of taking the process down. Idle unless
#                               something registers one.
#
# Env-var keys use __ for the : separator; the host reads DOTNET_-prefixed
# variables into `hostBuilder:reloadConfigOnChange`.
ENV DOTNET_hostBuilder__reloadConfigOnChange=false \
    DOTNET_USE_POLLING_FILE_WATCHER=true

# Loopback-only: nothing outside the container can reach Kestrel directly.
ENV ASPNETCORE_URLS=http://127.0.0.1:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    API_PORT=8080 \
    API_BASE_URL=/api \
    PORT=10000

# Informational — Render reads the actual listen port from $PORT at runtime.
EXPOSE 10000

# Runs as root so nginx can bind and write /var/log/nginx and /run/nginx.pid;
# the nginx master immediately drops its worker processes to www-data, per the
# `user` directive in Debian's /etc/nginx/nginx.conf.
ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]
