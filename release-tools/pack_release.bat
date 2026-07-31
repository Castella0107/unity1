@echo off
setlocal
REM ============================================================================
REM PVPharmonics - Velopack release packager (docs/deployment/velopack_release.md)
REM
REM Usage:   pack_release.bat <UnityBuildDir> <Version>
REM Example: pack_release.bat C:\Builds\pvp_win 0.2.0
REM
REM Requires: vpk ( dotnet tool update -g vpk )
REM Output:  .\releases\  (upload everything in it to ConoHa ~/updates/)
REM ============================================================================

if "%~2"=="" (
    echo Usage:   pack_release.bat ^<UnityBuildDir^> ^<Version^>
    echo Example: pack_release.bat C:\Builds\pvp_win 0.2.0
    exit /b 1
)

set BUILD_DIR=%~1
set VERSION=%~2
set PACK_ID=PVPharmonics
set MAIN_EXE=PVP.exe
set OUT_DIR=%~dp0releases
REM Must match VelopackAutoUpdater.FeedUrl / caddy_updates.md
set FEED_URL=https://pvpharmonics.duckdns.org/updates-x7q2mkv9tr4w/

if not exist "%BUILD_DIR%\%MAIN_EXE%" (
    echo [ERROR] %BUILD_DIR%\%MAIN_EXE% not found. Pass the Unity build output folder.
    exit /b 1
)

echo [1/3] Downloading existing releases for delta base (failure is OK on first release)...
vpk download http --url %FEED_URL% -o "%OUT_DIR%"
if errorlevel 1 echo   (download failed - if this is the first release, continue; full package only)

echo [2/3] Packing Velopack release v%VERSION% ...
vpk pack -u %PACK_ID% -v %VERSION% -p "%BUILD_DIR%" -e %MAIN_EXE% -o "%OUT_DIR%" --packTitle PVPharmonics
if errorlevel 1 (
    echo [ERROR] vpk pack failed
    exit /b 1
)

echo [3/3] Done. Upload the contents of %OUT_DIR% to ConoHa:
echo.
echo   (from WSL) scp -r /mnt/c/Users/mashi/projects/unity1/release-tools/releases/* kani@160.251.231.181:~/updates/
echo.
dir /b "%OUT_DIR%"
endlocal
