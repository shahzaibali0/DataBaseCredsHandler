# Refuses a commit that would publish the owner's data. Run by .githooks/pre-commit.
#
# 1. Blocks the app's data files and real environment files by name.
# 2. Blocks added lines that contain a project name, project path, database name or remote host that
#    PgCreds Handler found on THIS PC. The list is read at commit time from %APPDATA%\PgCredsHandler,
#    so the private names themselves are never stored in the repository.
# ASCII only: Windows PowerShell 5.1 reads scripts without a byte-order mark as ANSI.

$ErrorActionPreference = 'Stop'

$blockedNames = @('connections.json', 'settings.json', 'inventory.json', 'servers.json')
$blockedPatterns = @('*.backup-*', '*.corrupt-*', '*.db', 'pgcreds-*.json')
$genericWords = @('backend', 'frontend', 'postgres', 'localhost', 'template1', 'client', 'server', 'admin', 'web', 'api',
                  'app', 'src', 'test', 'tests', 'main', 'database', 'default', 'public', 'shop', 'store', 'pgcredshandler')

$staged = @(git diff --cached --name-only --diff-filter=ACMR)
$problems = New-Object System.Collections.Generic.List[string]

foreach ($path in $staged) {
    $name = Split-Path $path -Leaf
    $isEnvFile = ($name -eq '.env' -or $name -like '.env.*') -and $name -notmatch '\.(example|sample|template)$'
    $isBlocked = ($blockedNames -contains $name.ToLowerInvariant()) -or $isEnvFile
    foreach ($pattern in $blockedPatterns) { if ($name -like $pattern) { $isBlocked = $true } }
    if ($isBlocked) { $problems.Add("file '$path' looks like app data or a real .env file") }
}

function Add-Words($words, $value) {
    if ([string]::IsNullOrWhiteSpace($value)) { return }
    $trimmed = $value.Trim()
    if ($trimmed.Length -lt 4) { return }
    if ($genericWords -contains $trimmed.ToLowerInvariant()) { return }
    [void]$words.Add($trimmed)
    if ($trimmed.Contains('\')) { [void]$words.Add($trimmed.Replace('\', '/')) }
}

$privateWords = New-Object System.Collections.Generic.HashSet[string] ([StringComparer]::OrdinalIgnoreCase)
$dataFolder = Join-Path $env:APPDATA 'PgCredsHandler'
$connectionsFile = Join-Path $dataFolder 'connections.json'
$inventoryFile = Join-Path $dataFolder 'inventory.json'

if (Test-Path $connectionsFile) {
    $connections = Get-Content -Raw -Encoding UTF8 $connectionsFile | ConvertFrom-Json
    foreach ($connection in $connections) {
        Add-Words $privateWords $connection.projectName
        Add-Words $privateWords $connection.projectPath
        Add-Words $privateWords $connection.database
        if ($connection.host -notin @('localhost', '127.0.0.1', '::1', 'db', 'postgres')) { Add-Words $privateWords $connection.host }
    }
}
if (Test-Path $inventoryFile) {
    $inventory = Get-Content -Raw -Encoding UTF8 $inventoryFile | ConvertFrom-Json
    foreach ($app in $inventory.apps) {
        Add-Words $privateWords $app.workspaceName
        Add-Words $privateWords $app.workspacePath
        Add-Words $privateWords $app.appName
        Add-Words $privateWords $app.appPath
    }
}

if ($privateWords.Count -gt 0) {
    $addedLines = @(git diff --cached -U0 --diff-filter=ACMR | Where-Object { $_.StartsWith('+') -and -not $_.StartsWith('+++') })
    foreach ($word in $privateWords) {
        foreach ($line in $addedLines) {
            if ($line.IndexOf($word, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                $problems.Add("an added line contains your project data: '$word'")
                break
            }
        }
    }
}

if ($problems.Count -eq 0) { exit 0 }

Write-Host ''
Write-Host 'Commit blocked: it would publish your PgCreds Handler data.' -ForegroundColor Red
foreach ($problem in $problems) { Write-Host "  - $problem" }
Write-Host ''
Write-Host 'Replace real project names, paths and databases with made-up ones (e.g. ShopRepo, shop_db), then commit again.'
exit 1
