$ErrorActionPreference = "Stop"
$distDir = (Resolve-Path "$PSScriptRoot\..\dist").Path
$certPath = "$distDir\EyeRest-Test.pfx"
$certPassword = "EyeRestDev"
$msixPath = "$distDir\EyeRest.msix"
$subject = "CN=2C751A87-0159-47D5-93AD-8967182E99BD"

# --- Step 1: Create self-signed certificate using pure .NET ---
Write-Host "Creating self-signed certificate..." -ForegroundColor Cyan

Add-Type -AssemblyName System.Security

$oidCollection = New-Object System.Security.Cryptography.OidCollection
$oidCollection.Add((New-Object System.Security.Cryptography.Oid "1.3.6.1.5.5.7.3.3")) | Out-Null

$extension = New-Object System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension($oidCollection, $true)

$rsa = [System.Security.Cryptography.RSA]::Create(2048)
$request = New-Object System.Security.Cryptography.X509Certificates.CertificateRequest(
    $subject, $rsa,
    [System.Security.Cryptography.HashAlgorithmName]::SHA256,
    [System.Security.Cryptography.RSASignaturePadding]::Pkcs1
)
$request.CertificateExtensions.Add($extension)

$notBefore = [DateTimeOffset]::UtcNow.AddDays(-1)
$notAfter  = [DateTimeOffset]::UtcNow.AddYears(1)
$cert = $request.CreateSelfSigned($notBefore, $notAfter)

# Export to PFX using .NET (no ConvertTo-SecureString)
$pfxBytes = $cert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $certPassword)
[System.IO.File]::WriteAllBytes($certPath, $pfxBytes)
Write-Host "Certificate saved to: $certPath" -ForegroundColor Green

# --- Step 2: Install cert as trusted (try machine first, fall back to user) ---
Write-Host "Installing certificate as trusted..." -ForegroundColor Cyan
try {
    $store = New-Object System.Security.Cryptography.X509Certificates.X509Store(
        [System.Security.Cryptography.X509Certificates.StoreName]::TrustedPeople,
        [System.Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine
    )
    $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
    $store.Add($cert)
    $store.Close()
    Write-Host "Certificate installed to LocalMachine\TrustedPeople." -ForegroundColor Green
} catch {
    Write-Host "  Admin required for machine store. Trying current user..." -ForegroundColor Yellow
    $store = New-Object System.Security.Cryptography.X509Certificates.X509Store(
        [System.Security.Cryptography.X509Certificates.StoreName]::TrustedPeople,
        [System.Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser
    )
    $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
    $store.Add($cert)
    $store.Close()
    Write-Host "Certificate installed to CurrentUser\TrustedPeople." -ForegroundColor Green
}

# --- Step 3: Sign the MSIX ---
$sdkBinRoot = "C:\Program Files (x86)\Windows Kits\10\bin"
$signTool = Get-ChildItem "$sdkBinRoot\*\x64\signtool.exe" -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending |
    Select-Object -First 1

if ($signTool) {
    Write-Host "Signing MSIX..." -ForegroundColor Cyan
    & $signTool.FullName sign /fd SHA256 /a /f $certPath /p $certPassword $msixPath
    if ($LASTEXITCODE -eq 0) {
        Write-Host "`nDone! MSIX signed and certificate trusted." -ForegroundColor Green
        Write-Host "You can now double-click $msixPath to install." -ForegroundColor Green
    } else {
        Write-Error "Signing failed."
    }
} else {
    Write-Error "signtool.exe not found. Install the Windows SDK."
}
