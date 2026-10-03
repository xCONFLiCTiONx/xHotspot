param(
    [string]$Configuration = "Release",
    [string]$PublishDir = "$PSScriptRoot\..\publish"
)

Write-Host "Publishing xHotspot Service and App..." -ForegroundColor Cyan
dotnet publish "$PSScriptRoot\..\src\xHotspot.Service\xHotspot.Service.csproj" -c $Configuration -o "$PublishDir\Service"
dotnet publish "$PSScriptRoot\..\src\xHotspot.App\xHotspot.App.csproj" -c $Configuration -o "$PublishDir\App"
Write-Host "Publish completed to $PublishDir" -ForegroundColor Green
