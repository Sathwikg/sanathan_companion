@echo off
setlocal
REM Sanathana Companion - Web frontend (Blazor WebAssembly)
REM On this branch (development_mobileview) the web host renders the PHONE shell:
REM wwwroot/appsettings.json says Platform=Mobile, and phoneFrame.js frames it as a
REM 412x915 handset in a desktop-sized browser window. Add ?frame=0 for full width.
set ASPNETCORE_URLS=http://localhost:7001
cd /d "%~dp0FrontEnd\App.Web"
echo ============================================================
echo  Sanathana Companion phone shell (Blazor WASM) -> http://localhost:7001
echo  Renders the mobile app's UI, framed as a handset on a desktop window.
echo  (calls the API at http://localhost:7050/api - start RunBackend first)
echo ============================================================
dotnet run --no-launch-profile
endlocal
