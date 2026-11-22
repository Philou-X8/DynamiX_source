@echo off
rem Builds the self-contained win-x64 deploy version of the HMI into ..\publish
rem (next to the .sln). Double-click it, or run it from any folder.
rem English only: SatelliteResourceLanguages=en skips the cs/de/fr/... resource folders.
rem Note: it doesn't empty the publish folder first (SavedRuns lives in there).

pushd "%~dp0"
dotnet publish -c Release -r win-x64 --self-contained true -p:SatelliteResourceLanguages=en -o "%~dp0..\publish"
set RESULT=%ERRORLEVEL%
popd

echo.
if %RESULT%==0 (
    echo Publish OK: %~dp0..\publish
) else (
    echo Publish FAILED with exit code %RESULT%
)
exit /b %RESULT%
