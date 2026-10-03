# Requires Administrator privileges
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "This script must be run as Administrator."
    exit 1
}

$ServiceName = "xHotspot"
$DisplayName = "xHotspot Automatic Hotspot Manager"
$BinPath = "$PSScriptRoot\..\src\xHotspot.Service\bin\Release\net8.0-windows\xHotspot.Service.exe"

if (-not (Test-Path $BinPath)) {
    Write-Warning "Release binary not found. Building project..."
    & "$PSScriptRoot\Build.ps1" -Configuration "Release"
}

Write-Host "Installing Windows Service: $ServiceName..." -ForegroundColor Cyan
New-Service -Name $ServiceName -DisplayName $DisplayName -BinaryPathName "$BinPath" -StartupType Automatic -Description "Automatically manages Windows Mobile Hotspot based on phone presence and network state."

# Configure service recovery
sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000

Write-Host "Service installed and configured successfully." -ForegroundColor Green
