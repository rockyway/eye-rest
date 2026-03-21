<#
.SYNOPSIS
    Publish Eye-Rest for Windows via Velopack.
.PARAMETER Version
    Version number (e.g., 1.0.2). If omitted, reads from Directory.Build.props.
.PARAMETER AzureTrustedSignFile
    Path to Azure Trusted Signing metadata.json (recommended).
    Requires AZURE_CLIENT_ID, AZURE_CLIENT_SECRET, AZURE_TENANT_ID env vars.
.PARAMETER SignParams
    Authenticode signing parameters passed directly to vpk.
    e.g. '/a /f cert.pfx /p password /fd sha256 /tr http://timestamp.digicert.com /td sha256'
.EXAMPLE
    .\publish-velopack-win.ps1              # auto-detects version
    .\publish-velopack-win.ps1 -Version 1.0.3
.EXAMPLE
    .\publish-velopack-win.ps1 -Version 1.0.2 -AzureTrustedSignFile ..\signing\metadata.json
#>
param(
    [string]$Version              = "",
    [string]$Configuration        = "Release",
    [string]$AzureTrustedSignFile = "",
    [string]$SignParams           = ""
)

$ErrorActionPreference = "Stop"
$SolutionRoot = Split-Path $PSScriptRoot -Parent

# Resolve version: explicit arg > Directory.Build.props > error
if (-not $Version) {
    $propsFile = "$SolutionRoot\Directory.Build.props"
    $Version = ([xml](Get-Content $propsFile)).Project.PropertyGroup.Version |
               Where-Object { $_ } | Select-Object -First 1
    if (-not $Version) {
        Write-Error "Could not read <Version> from $propsFile. Pass -Version explicitly."
        exit 1
    }
}

$PublishDir   = "$SolutionRoot\publish\velopack-win"
$ReleasesDir  = "$SolutionRoot\releases"
$SigningDir   = "$SolutionRoot\signing"
$Icon         = "$SolutionRoot\Resources\app.ico"

# --- Auto-load signing credentials -----------------------------------------------
# Loads Azure credentials from signing\.env and sets AzureTrustedSignFile to
# signing\metadata.json if both exist and no signing params are already set.
if ($AzureTrustedSignFile -eq "" -and $SignParams -eq "") {
    $EnvFile      = Join-Path $SigningDir ".env"
    $MetadataFile = Join-Path $SigningDir "metadata.json"
    if ((Test-Path $EnvFile) -and (Test-Path $MetadataFile)) {
        Write-Host "=== Loading signing credentials from signing\.env ===" -ForegroundColor Yellow
        Get-Content $EnvFile | ForEach-Object {
            if ($_ -match '^\s*([^#][^=]+)=(.*)$') {
                [System.Environment]::SetEnvironmentVariable($Matches[1].Trim(), $Matches[2].Trim(), "Process")
            }
        }
        if ($env:AZURE_CLIENT_ID -and $env:AZURE_CLIENT_SECRET -and $env:AZURE_TENANT_ID) {
            $AzureTrustedSignFile = $MetadataFile
            Write-Host "    Azure credentials loaded"
        } else {
            Write-Host "    Warning: signing\.env found but credentials are incomplete - building unsigned" -ForegroundColor DarkYellow
        }
    }
}

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

$VpkArgs = @(
    "pack"
    "-u",  "EyeRest"
    "-v",  $Version
    "-p",  $PublishDir
    "-e",  "EyeRest.exe"
    "--icon", $Icon
    "-o",  $ReleasesDir
)

if ($AzureTrustedSignFile -ne "") {
    Write-Host "    Signing mode : Azure Trusted Signing (immediate SmartScreen trust)" -ForegroundColor Green
    Write-Host "    Sign scope   : All unsigned binaries (runtime DLLs already vendor-signed)"
    $VpkArgs += "--azureTrustedSignFile", $AzureTrustedSignFile
} elseif ($SignParams -ne "") {
    Write-Host '    Signing mode : Authenticode (signParams provided)' -ForegroundColor Green
    Write-Host '    Sign scope   : All unsigned binaries (runtime DLLs already vendor-signed)'
    $VpkArgs += "--signParams", $SignParams
} else {
    Write-Host '    Signing mode : Unsigned' -ForegroundColor DarkYellow
    Write-Host '    Tip: Place Azure credentials in signing\.env + signing\metadata.json for auto-signing'
    Write-Host '         or pass -AzureTrustedSignFile or -SignParams'
}

$vpkPath = Join-Path $env:USERPROFILE ".dotnet\tools\vpk.exe"
if (-not (Test-Path $vpkPath)) { $vpkPath = "vpk" }
& $vpkPath @VpkArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "vpk pack failed"
    exit 1
}

# Step 3: Summary
Write-Host "`n[3/3] Done!" -ForegroundColor Green
Write-Host "  Releases in: $ReleasesDir"
Get-ChildItem $ReleasesDir | Format-Table Name, @{L='Size';E={'{0:N1} MB' -f ($_.Length / 1MB)}} -AutoSize
