$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "src\HighlandMilkBar.WPF\HighlandMilkBar.WPF.csproj"

Write-Host "Highland Milk Bar Management System" -ForegroundColor Cyan
Write-Host "Checking .NET 10 SDK..." -ForegroundColor Gray

$sdks = dotnet --list-sdks
if ($LASTEXITCODE -ne 0 -or -not ($sdks -match '^10\.')) {
    Write-Host "The .NET 10 SDK is required. Install it from https://dotnet.microsoft.com/download/dotnet/10.0" -ForegroundColor Yellow
    exit 1
}

Write-Host "Restoring NuGet packages..." -ForegroundColor Gray
dotnet restore $project
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Launching the application..." -ForegroundColor Gray
dotnet run --project $project
exit $LASTEXITCODE
