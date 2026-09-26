@echo off
setlocal
REM Sanathana Companion - Backend API (.NET 10, PostgreSQL on localhost)
set ASPNETCORE_ENVIRONMENT=Development
set ASPNETCORE_URLS=http://localhost:7050
REM Default on this branch: the local PostgreSQL service. appsettings.Development.json names the
REM Supabase direct host, which is IPv6-only and times out on a machine without IPv6 egress.
REM Set ConnectionStrings__DefaultConnection before calling this script to use another database.
if not defined ConnectionStrings__DefaultConnection set "ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=sanathana_companion;Username=postgres;Password=postgres"
cd /d "%~dp0BackEnd\src\Sanathana.Companion.Api"
echo ============================================================
echo  Sanathana Companion API   -^>  http://localhost:7050
echo  Swagger UI                -^>  http://localhost:7050/swagger
echo  Database: local PostgreSQL on localhost:5432 (sanathana_companion)
echo ============================================================
dotnet run --no-launch-profile
endlocal
