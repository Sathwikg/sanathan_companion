@echo off
setlocal
REM Nitya Sannidhi - Backend API (.NET 10, PostgreSQL on localhost)
set ASPNETCORE_ENVIRONMENT=Development
set ASPNETCORE_URLS=http://localhost:7050
cd /d "%~dp0BackEnd\src\Sanathana.Companion.Api"
echo ============================================================
echo  Nitya Sannidhi API   ->  http://localhost:7050
echo  Swagger UI                ->  http://localhost:7050/swagger
echo  (requires PostgreSQL running on localhost:5432)
echo ============================================================
dotnet run --no-launch-profile
endlocal
