@echo off
setlocal DisableDelayedExpansion
title Veda launcher

rem Keep this file in the Veda source folder. Paths may contain spaces.
if not exist "%~dp0src\Veda.App\Veda.App.csproj" goto missingProject
where dotnet >nul 2>nul
if errorlevel 1 goto missingSdk

pushd "%~dp0"
if errorlevel 1 goto folderFailed
echo Starting Veda. The first launch may take longer while packages download.
echo Keep this window open while Veda is running.
echo.
dotnet run --project "src\Veda.App\Veda.App.csproj" -c Debug -p:Platform=x64
set "VedaExitCode=%errorlevel%"
popd
if not "%VedaExitCode%"=="0" goto runFailed
exit /b 0

:missingProject
echo Veda's source files could not be found.
echo Extract the ZIP first, then keep Run-Veda.bat inside the Veda folder.
echo Create a desktop shortcut to this file rather than moving it.
pause
exit /b 1

:missingSdk
echo The .NET SDK could not be found.
echo Install the .NET 10 SDK and Windows development tools described in START_HERE.md.
echo Then close this window and double-click Run-Veda.bat again.
pause
exit /b 1

:folderFailed
echo The Veda folder could not be opened. Check its location and permissions.
pause
exit /b 1

:runFailed
echo.
echo Veda could not build or exited with an error. The details are above.
echo Confirm the .NET 10 SDK and WinUI development tools are installed.
echo See START_HERE.md for setup, or copy the error text when asking for help.
pause
exit /b %VedaExitCode%
