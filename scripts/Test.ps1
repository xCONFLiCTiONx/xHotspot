Write-Host "Running xHotspot unit tests..." -ForegroundColor Cyan
dotnet test "$PSScriptRoot\..\tests\xHotspot.Tests\xHotspot.Tests.csproj" --no-build --verbosity normal
if ($LASTEXITCODE -ne 0) {
    Write-Error "Tests failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}
Write-Host "All tests passed successfully." -ForegroundColor Green
