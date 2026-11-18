@echo off
rem Builds and starts DB Connect Hub. Double-click this file after cloning the repository.
setlocal
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo The .NET SDK was not found on this PC.
    echo Install .NET 8 or newer from https://dotnet.microsoft.com/download and run start.cmd again.
    pause
    exit /b 1
)

rem The app keeps running in the tray after its window is closed, and a running app's files can't be
rem replaced by a build. Close it first; the fresh build is started at the end.
tasklist /FI "IMAGENAME eq DbConnectHub.exe" 2>nul | find /I "DbConnectHub.exe" >nul
if not errorlevel 1 (
    echo Closing the running DB Connect Hub so it can be rebuilt...
    taskkill /IM DbConnectHub.exe /F >nul 2>nul
    rem Wait about 2 seconds for Windows to release the files (ping works even without a console, unlike timeout).
    ping -n 3 127.0.0.1 >nul
)

echo Building DB Connect Hub...
dotnet build src\DbConnectHub.App -c Release --nologo -v quiet
if errorlevel 1 (
    echo.
    echo The build failed. The messages above say why.
    pause
    exit /b 1
)

start "" "src\DbConnectHub.App\bin\Release\net8.0-windows10.0.19041.0\win-x64\DbConnectHub.exe"
