param(
    [string]$Configuration = "Release",
    [string]$Version = "1.0.0.0",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"
$Root = Split-Path $PSScriptRoot -Parent
$PackagingDir = $PSScriptRoot
$PublishDir = Join-Path $Root "publish\msix-win-x64"
$LayoutDir = Join-Path $PackagingDir "layout"
$OutputMsix = Join-Path $Root "publish\VaultArc-$Version-win-x64.msix"

Write-Host "=== VaultArc MSIX Build ===" -ForegroundColor Cyan
Write-Host "Version:       $Version"
Write-Host "Configuration: $Configuration"
Write-Host ""

# Step 1: Publish the Avalonia app
if (-not $SkipPublish) {
    Write-Host "[1/4] Publishing VaultArc.Avalonia..." -ForegroundColor Yellow
    dotnet publish "$Root\VaultArc.Avalonia\VaultArc.Avalonia.csproj" `
        -c $Configuration -r win-x64 --self-contained `
        -o $PublishDir
    if ($LASTEXITCODE -ne 0) { throw "Publish failed" }
} else {
    Write-Host "[1/4] Skipping publish (using existing output)" -ForegroundColor DarkGray
}

# Step 2: Create MSIX layout directory
Write-Host "[2/4] Creating MSIX layout..." -ForegroundColor Yellow
if (Test-Path $LayoutDir) { Remove-Item $LayoutDir -Recurse -Force }
New-Item $LayoutDir -ItemType Directory -Force | Out-Null

Copy-Item "$PublishDir\*" $LayoutDir -Recurse -Force
Copy-Item "$PackagingDir\Package.appxmanifest" "$LayoutDir\AppxManifest.xml" -Force
New-Item "$LayoutDir\Images" -ItemType Directory -Force | Out-Null
Copy-Item "$PackagingDir\Images\*" "$LayoutDir\Images\" -Force

# Step 3: Locate makeappx.exe from Windows SDK
Write-Host "[3/4] Packaging MSIX..." -ForegroundColor Yellow
$sdkBinPaths = @(
    "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\makeappx.exe",
    "$env:ProgramFiles\Windows Kits\10\bin\*\x64\makeappx.exe"
)
$makeAppx = $null
foreach ($pattern in $sdkBinPaths) {
    $found = Get-Item $pattern -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1
    if ($found) { $makeAppx = $found.FullName; break }
}
if (-not $makeAppx) {
    throw "makeappx.exe not found. Install the Windows 10/11 SDK: winget install Microsoft.WindowsSDK.10.0.26100"
}
Write-Host "  Using: $makeAppx"

New-Item (Split-Path $OutputMsix) -ItemType Directory -Force | Out-Null
& $makeAppx pack /d $LayoutDir /p $OutputMsix /o
if ($LASTEXITCODE -ne 0) { throw "makeappx pack failed" }

# Step 4: Signing instructions
Write-Host ""
Write-Host "[4/4] MSIX created: $OutputMsix" -ForegroundColor Green
Write-Host ""
Write-Host "--- Signing (required for installation) ---" -ForegroundColor Cyan
Write-Host "To sign with a self-signed cert for local testing:"
Write-Host ""
Write-Host '  # Create a self-signed certificate (one time):'
Write-Host '  $cert = New-SelfSignedCertificate -Type Custom -Subject "CN=VaultArc" \'
Write-Host '    -KeyUsage DigitalSignature -FriendlyName "VaultArc Dev" \'
Write-Host '    -CertStoreLocation "Cert:\CurrentUser\My" \'
Write-Host '    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3")'
Write-Host ""
Write-Host '  # Trust it (one time):'
Write-Host '  Export-Certificate -Cert $cert -FilePath VaultArc-dev.cer'
Write-Host '  Import-Certificate -FilePath VaultArc-dev.cer -CertStoreLocation "Cert:\LocalMachine\TrustedPeople"'
Write-Host ""
Write-Host "  # Sign the MSIX:"
Write-Host "  signtool sign /fd SHA256 /a /f <your.pfx> /p <password> `"$OutputMsix`""
Write-Host ""
Write-Host "After signing, install with: Add-AppPackage -Path `"$OutputMsix`""
Write-Host ""

Write-Host "=== Done ===" -ForegroundColor Cyan
