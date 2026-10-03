if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "This script must be run as Administrator."
    exit 1
}
Stop-Service -Name "xHotspot" -ErrorAction SilentlyContinue
Write-Host "xHotspot service stopped." -ForegroundColor Green
