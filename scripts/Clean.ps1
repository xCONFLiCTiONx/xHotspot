Write-Host "Cleaning xHotspot solution..." -ForegroundColor Cyan
dotnet clean "$PSScriptRoot\..\xHotspot.sln"
Remove-Item -Recurse -Force "$PSScriptRoot\..\src\*\bin" -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force "$PSScriptRoot\..\src\*\obj" -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force "$PSScriptRoot\..\tests\*\bin" -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force "$PSScriptRoot\..\tests\*\obj" -ErrorAction SilentlyContinue
Write-Host "Clean completed." -ForegroundColor Green
