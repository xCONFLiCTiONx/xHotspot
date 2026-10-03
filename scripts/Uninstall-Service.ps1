if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "This script must be run as Administrator."
    exit 1
}

$ServiceName = "xHotspot"
Write-Host "Stopping and uninstalling Windows Service: $ServiceName..." -ForegroundColor Cyan

Stop-Service -Name $ServiceName -ErrorAction SilentlyContinue
sc.exe delete $ServiceName

Write-Host "Service uninstalled successfully." -ForegroundColor Green
