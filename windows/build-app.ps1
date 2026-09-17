<#
.SYNOPSIS
    Builds On-Screen Chat into a self-contained folder you can run or copy.

.DESCRIPTION
    The Windows counterpart of the Mac app's ./build-app.sh. Publishes the WinUI
    app unpackaged and self-contained — no MSIX, no store, and the machine does
    not need the Windows App SDK runtime installed — then copies your local .env
    to where the app reads it.

.PARAMETER Configuration
    Debug or Release. Defaults to Release.

.PARAMETER Runtime
    win-x64 or win-arm64. Defaults to win-x64.

.EXAMPLE
    .\build-app.ps1
    .\publish\OnScreenChat.exe
#>

[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
Set-Location -Path $PSScriptRoot

$output = Join-Path $PSScriptRoot 'publish'

Write-Host "Building On-Screen Chat ($Configuration / $Runtime)..." -ForegroundColor Cyan

# Core logic is covered by tests that run anywhere; fail early if they don't pass.
dotnet test .\OnScreenChat.Core.slnf --configuration $Configuration --nologo
if ($LASTEXITCODE -ne 0) { throw "Tests failed - not publishing." }

if (Test-Path $output) { Remove-Item $output -Recurse -Force }

dotnet publish .\src\OnScreenChat\OnScreenChat.csproj `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    --output $output `
    --nologo
if ($LASTEXITCODE -ne 0) { throw "Publish failed." }

# Propagate the local .env (secrets) to where the app reads it. NOT copied into
# the published folder, so the key is never shipped or shared.
$appData = Join-Path $env:LOCALAPPDATA 'OnScreenChat'
New-Item -ItemType Directory -Path $appData -Force | Out-Null

if (Test-Path .\.env) {
    Copy-Item .\.env (Join-Path $appData '.env') -Force
    Write-Host "Copied .env -> $appData\.env" -ForegroundColor Green
}
else {
    Write-Host "No .env found. Copy .env.example to .env and add your Gemini key," -ForegroundColor Yellow
    Write-Host "  otherwise every send will show the missing-key notice." -ForegroundColor Yellow
}

$exe = Join-Path $output 'OnScreenChat.exe'
Write-Host ""
Write-Host "Built $exe" -ForegroundColor Green
Write-Host "   Launch with:  .\publish\OnScreenChat.exe"
Write-Host "   Toggle with:  Ctrl+Shift+Space"
