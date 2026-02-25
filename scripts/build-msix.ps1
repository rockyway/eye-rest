<#
.SYNOPSIS
    Build an MSIX package for Eye-Rest (Microsoft Store or sideloading).

.DESCRIPTION
    Publishes the EyeRest.UI project as self-contained, generates MSIX icons,
    then packs everything into an .msix using makeappx.

.PARAMETER Configuration
    Build configuration (default: Release).

.PARAMETER ForStore
    If set, produces an unsigned MSIX for Store upload.
    Otherwise, signs with a self-signed test certificate for local testing.

.EXAMPLE
    # For Microsoft Store submission (unsigned):
    .\build-msix.ps1 -ForStore

    # For local sideload testing:
    .\build-msix.ps1
#>
param(
    [string]$Configuration = "Release",
    [switch]$ForStore
)

$ErrorActionPreference = "Stop"
$SolutionRoot = Split-Path $PSScriptRoot -Parent
$PublishDir = "$SolutionRoot\publish\win-x64"
$DistDir = "$SolutionRoot\dist"
$PackageDir = "$SolutionRoot\EyeRest.Package"
$MsixPath = "$DistDir\EyeRest.msix"

# --- Step 1: Generate MSIX icons ---
Write-Host "`n[1/5] Generating MSIX icons..." -ForegroundColor Cyan
python "$SolutionRoot\scripts\generate-icons.py" --msix
if ($LASTEXITCODE -ne 0) {
    Write-Error "Icon generation failed"
    exit 1
}

# --- Step 2: Publish the app ---
Write-Host "`n[2/5] Publishing EyeRest.UI (self-contained, win-x64)..." -ForegroundColor Cyan
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

# --- Step 3: Copy manifest and assets into publish folder ---
Write-Host "`n[3/5] Preparing MSIX layout..." -ForegroundColor Cyan
Copy-Item "$PackageDir\Package.appxmanifest" "$PublishDir\AppxManifest.xml" -Force

# Create Images directory in publish output
$ImagesDir = "$PublishDir\Images"
if (Test-Path $ImagesDir) { Remove-Item $ImagesDir -Recurse -Force }
Copy-Item "$PackageDir\Images" $ImagesDir -Recurse

# Also ensure Resources/app.ico is present
if (Test-Path "$SolutionRoot\Resources\app.ico") {
    $ResourcesDir = "$PublishDir\Resources"
    if (-not (Test-Path $ResourcesDir)) { New-Item -ItemType Directory -Path $ResourcesDir | Out-Null }
    Copy-Item "$SolutionRoot\Resources\app.ico" "$ResourcesDir\app.ico" -Force
}

# --- Step 4: Pack into MSIX ---
Write-Host "`n[4/5] Creating MSIX package..." -ForegroundColor Cyan
if (-not (Test-Path $DistDir)) { New-Item -ItemType Directory -Path $DistDir | Out-Null }
if (Test-Path $MsixPath) { Remove-Item $MsixPath -Force }

# Find makeappx.exe in Windows SDK
$SdkBinRoot = "C:\Program Files (x86)\Windows Kits\10\bin"
$MakeAppx = Get-ChildItem "$SdkBinRoot\*\x64\makeappx.exe" -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending |
    Select-Object -First 1

if (-not $MakeAppx) {
    Write-Error "makeappx.exe not found. Install the Windows 10/11 SDK."
    exit 1
}

Write-Host "  Using: $($MakeAppx.FullName)"
& $MakeAppx.FullName pack /d $PublishDir /p $MsixPath /o
if ($LASTEXITCODE -ne 0) {
    Write-Error "makeappx pack failed"
    exit 1
}

# --- Step 5: Sign (for sideloading only) ---
if (-not $ForStore) {
    Write-Host "`n[5/5] Signing MSIX for local testing..." -ForegroundColor Cyan

    $CertPath = "$DistDir\EyeRest-Test.pfx"
    $CertPassword = "EyeRestDev"

    # Create a self-signed test certificate if it doesn't exist
    if (-not (Test-Path $CertPath)) {
        try {
            Write-Host "  Creating self-signed test certificate..."
            $cert = New-SelfSignedCertificate `
                -Type Custom `
                -Subject "CN=EyeRest" `
                -KeyUsage DigitalSignature `
                -FriendlyName "EyeRest Dev Test" `
                -CertStoreLocation "Cert:\CurrentUser\My" `
                -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

            $securePassword = ConvertTo-SecureString -String $CertPassword -Force -AsPlainText
            Export-PfxCertificate -Cert $cert -FilePath $CertPath -Password $securePassword | Out-Null
            Write-Host "  Test certificate saved to: $CertPath"
        }
        catch {
            Write-Warning "Could not create self-signed certificate: $_"
            Write-Warning "Run this script from Windows PowerShell (not Git Bash) for signing support."
            Write-Host "  The unsigned MSIX at $MsixPath is still valid for Store upload." -ForegroundColor Yellow
        }
    }

    if (Test-Path $CertPath) {
        # Find signtool.exe in Windows SDK
        $SignTool = Get-ChildItem "$SdkBinRoot\*\x64\signtool.exe" -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending |
            Select-Object -First 1

        if ($SignTool) {
            & $SignTool.FullName sign /fd SHA256 /a /f $CertPath /p $CertPassword $MsixPath
            if ($LASTEXITCODE -ne 0) {
                Write-Warning "Signing failed. The MSIX is still usable for Store upload."
            }
        } else {
            Write-Warning "signtool.exe not found. Skipping signing."
        }
    }
} else {
    Write-Host "`n[5/5] Skipping signing (Store upload - Microsoft will sign it)." -ForegroundColor Cyan
}

# --- Summary ---
$fileSize = (Get-Item $MsixPath).Length / 1MB
Write-Host "`n========================================" -ForegroundColor Green
Write-Host "  MSIX package created successfully!" -ForegroundColor Green
Write-Host "  Path: $MsixPath" -ForegroundColor Green
Write-Host "  Size: $([math]::Round($fileSize, 1)) MB" -ForegroundColor Green
if ($ForStore) {
    Write-Host "  Mode: Unsigned (ready for Store upload)" -ForegroundColor Yellow
} else {
    Write-Host "  Mode: Self-signed (for local sideload testing)" -ForegroundColor Yellow
}
Write-Host "========================================`n" -ForegroundColor Green
