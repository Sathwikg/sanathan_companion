@echo off
setlocal
REM Sanathana Companion - Web frontend (Blazor WebAssembly)
set ASPNETCORE_URLS=http://localhost:7001
cd /d "%~dp0FrontEnd\App.Web"
echo ============================================================
echo  Sanathana Companion Web (Blazor WASM) -> http://localhost:7001
echo  (calls the API at http://localhost:7050/api - start RunBackend first)
echo ============================================================
dotnet run --no-launch-profile
endlocal
