@echo off
setlocal
REM Sanathana Companion - Web frontend (Blazor WebAssembly)
REM On this branch (development_mobileview) the web host renders the PHONE shell:
REM wwwroot/appsettings.json says Platform=Mobile. The "http" launch profile opens the browser
REM at the full-width address once the server is listening, so the app fills the window.
REM Open the plain address to see it framed as a 412x915 handset (phoneFrame.js).
REM Note: a REM line must never start with a slash and question mark; cmd treats it as a help request.
cd /d "%~dp0FrontEnd\App.Web"
echo ============================================================
echo  Sanathana Companion phone shell (Blazor WASM)
echo    Full width  -^> http://localhost:7001/?frame=0   (opens automatically)
echo    Handset     -^> http://localhost:7001
echo  (calls the API at http://localhost:7050/api - start RunBackend first)
echo ============================================================
dotnet run --launch-profile http
endlocal
