<#
.SYNOPSIS
    Publish Eye-Rest for Windows via Velopack.
.PARAMETER Version
    Version number (e.g., 1.0.2). If omitted, reads from Directory.Build.props.
.EXAMPLE
    .\publish-velopack-win.ps1              # auto-detects version
    .\publish-velopack-win.ps1 -Version 1.0.3
#>
param(
    [string]$Version = "",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$SolutionRoot = Split-Path $PSScriptRoot -Parent

# Resolve version: explicit arg → Directory.Build.props → error
if (-not $Version) {
    $propsFile = "$SolutionRoot\Directory.Build.props"
    $Version = ([xml](Get-Content $propsFile)).Project.PropertyGroup.Version |
               Where-Object { $_ } | Select-Object -First 1
    if (-not $Version) {
        Write-Error "Could not read <Version> from $propsFile. Pass -Version explicitly."
        exit 1
    }
}

$PublishDir = "$SolutionRoot\publish\velopack-win"
$ReleasesDir = "$SolutionRoot\releases"

Write-Host "`n=== Eye-Rest Velopack Publish (Windows) ===" -ForegroundColor Cyan
Write-Host "  Version: $Version"

# Step 1: Publish self-contained
Write-Host "`n[1/3] Publishing..." -ForegroundColor Cyan
dotnet publish "$SolutionRoot\EyeRest.UI\EyeRest.UI.csproj" `
    -c $Configuration `
    -r win-x64 `
    --self-contained `
    -p:PublishSingleFile=false `
    -p:PublishTrimmed=false `
    -o $PublishDir
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed"
    exit 1
}

# Step 2: vpk pack
Write-Host "`n[2/3] Packing with vpk..." -ForegroundColor Cyan
if (-not (Test-Path $ReleasesDir)) { New-Item -ItemType Directory -Path $ReleasesDir | Out-Null }
vpk pack `
    -u EyeRest `
    -v $Version `
    -p $PublishDir `
    -e EyeRest.exe `
    -o $ReleasesDir
if ($LASTEXITCODE -ne 0) {
    Write-Error "vpk pack failed"
    exit 1
}

# Step 3: Summary
Write-Host "`n[3/3] Done!" -ForegroundColor Green
Write-Host "  Releases in: $ReleasesDir"
Get-ChildItem $ReleasesDir | ForEach-Object {
    Write-Host "    $($_.Name) ($([math]::Round($_.Length / 1MB, 1)) MB)"
}
