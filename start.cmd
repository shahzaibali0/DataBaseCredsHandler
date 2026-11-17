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

echo Building DB Connect Hub...
dotnet build src\DbConnectHub.App -c Release --nologo -v quiet
if errorlevel 1 (
    echo.
    echo The build failed. The messages above say why.
    pause
    exit /b 1
)

start "" "src\DbConnectHub.App\bin\Release\net8.0-windows10.0.19041.0\win-x64\DbConnectHub.exe"
