param(
    [string]$Configuration = "Release"
)

Write-Host "Building xHotspot ($Configuration)..." -ForegroundColor Cyan
dotnet build "$PSScriptRoot\..\xHotspot.sln" -c $Configuration
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}
Write-Host "Build completed successfully." -ForegroundColor Green
