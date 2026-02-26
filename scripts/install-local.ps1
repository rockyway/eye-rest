# Run this script as Administrator to install the MSIX locally
$ErrorActionPreference = "Stop"
$distDir = "$PSScriptRoot\..\dist"
$certPath = "$distDir\EyeRest-Test.pfx"
$msixPath = "$distDir\EyeRest.msix"

# Install cert to LocalMachine Trusted Root
Write-Host "Installing certificate to Trusted Root (requires admin)..." -ForegroundColor Cyan
Add-Type -AssemblyName System.Security
$pfxBytes = [System.IO.File]::ReadAllBytes($certPath)
$cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($pfxBytes, "EyeRestDev")

$store = New-Object System.Security.Cryptography.X509Certificates.X509Store(
    [System.Security.Cryptography.X509Certificates.StoreName]::Root,
    [System.Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine
)
$store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
$store.Add($cert)
$store.Close()
Write-Host "Certificate trusted." -ForegroundColor Green

# Install the MSIX
Write-Host "Installing MSIX package..." -ForegroundColor Cyan
Add-AppxPackage -Path $msixPath
Write-Host "`nEye-Rest installed successfully!" -ForegroundColor Green
Write-Host "You can find it in the Start Menu." -ForegroundColor Green
Read-Host "`nPress Enter to close"
