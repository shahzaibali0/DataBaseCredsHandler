# Installs or updates DB Connect Hub for the current Windows user. No admin rights needed.
#
#   irm https://raw.githubusercontent.com/shahzaibali0/DataBaseCredsHandler/main/install.ps1 | iex
#
# Downloads the latest release, puts it in %LOCALAPPDATA%\Programs\DbConnectHub, adds a Start menu
# shortcut and starts the app. Your saved projects and passwords are not touched by an update.
# To uninstall: run it with -Uninstall (see the README).
# ASCII only: Windows PowerShell 5.1 reads scripts without a byte-order mark as ANSI.

param([switch]$Uninstall)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'   # Invoke-WebRequest is far faster without the progress bar

$appName = 'DB Connect Hub'
$downloadUrl = 'https://github.com/shahzaibali0/DataBaseCredsHandler/releases/latest/download/DbConnectHub.exe'
$installFolder = Join-Path $env:LOCALAPPDATA 'Programs\DbConnectHub'
$exePath = Join-Path $installFolder 'DbConnectHub.exe'
$shortcutPath = Join-Path ([Environment]::GetFolderPath('Programs')) "$appName.lnk"

function Stop-RunningApp {
    Get-Process -Name 'DbConnectHub' -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 500
}

if ($Uninstall) {
    Stop-RunningApp
    Remove-Item $installFolder -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $shortcutPath -Force -ErrorAction SilentlyContinue
    Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name $appName -ErrorAction SilentlyContinue
    Write-Host "$appName is uninstalled." -ForegroundColor Green
    Write-Host "Your saved list is still in $env:APPDATA\DbConnectHub and the passwords in Credential Manager."
    Write-Host 'Delete them too if you want everything gone (see the README).'
    return
}

Write-Host "Installing $appName..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force $installFolder | Out-Null

$download = Join-Path $env:TEMP "DbConnectHub-$([guid]::NewGuid().ToString('N')).exe"
try {
    Invoke-WebRequest -Uri $downloadUrl -OutFile $download -UseBasicParsing
} catch {
    Write-Host "Couldn't download the app: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host 'Check your internet connection, or download DbConnectHub.exe from the Releases page on GitHub.'
    return
}

Stop-RunningApp   # an update can't replace the file while it is running
Move-Item $download $exePath -Force

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exePath
$shortcut.WorkingDirectory = $installFolder
$shortcut.Description = 'Your projects, ports and database connections, on this PC only'
$shortcut.Save()

Write-Host "$appName is installed. Find it in the Start menu any time." -ForegroundColor Green
Start-Process $exePath
