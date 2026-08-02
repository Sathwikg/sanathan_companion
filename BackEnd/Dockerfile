# syntax=docker/dockerfile:1
# Sanathana Companion API (.NET 10). Build context is the BackEnd/ folder.

# ---- build -----------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore against the csproj files alone so the NuGet layer survives source edits.
# Sanathana.Companion.Modules is in the solution but nothing references it, so the
# Api csproj restore covers everything that actually ships.
COPY Directory.Build.props ./
COPY src/Sanathana.Companion.Domain/Sanathana.Companion.Domain.csproj                 src/Sanathana.Companion.Domain/
COPY src/Sanathana.Companion.Application/Sanathana.Companion.Application.csproj       src/Sanathana.Companion.Application/
COPY src/Sanathana.Companion.Infrastructure/Sanathana.Companion.Infrastructure.csproj src/Sanathana.Companion.Infrastructure/
COPY src/Sanathana.Companion.Api/Sanathana.Companion.Api.csproj                       src/Sanathana.Companion.Api/
RUN dotnet restore src/Sanathana.Companion.Api/Sanathana.Companion.Api.csproj

COPY src/ src/
RUN dotnet publish src/Sanathana.Companion.Api/Sanathana.Companion.Api.csproj \
        -c Release \
        -o /app/publish \
        --no-restore \
        /p:UseAppHost=false

# ---- runtime ---------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# curl exists purely so compose can health-check the API; the aspnet image ships
# without any HTTP client.
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish ./

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# APP_UID is defined by the base image (non-root).
USER $APP_UID

ENTRYPOINT ["dotnet", "Sanathana.Companion.Api.dll"]
