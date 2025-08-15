# Clean test runner for ODTE project
# Suppresses verbose build output and shows only test results

param(
    [string]$Filter = "",
    [string]$Project = "ODTE.Strategy.Tests"
)

Write-Host "🧪 Running ODTE Tests..." -ForegroundColor Cyan

# Restore quietly first
Write-Host "📦 Restoring packages..." -ForegroundColor Yellow
dotnet restore --verbosity quiet 2>$null

# Run tests with minimal output
Write-Host "🔬 Executing tests..." -ForegroundColor Green

if ($Filter) {
    dotnet test $Project --no-restore --verbosity minimal --filter $Filter --logger "console;verbosity=normal"
} else {
    dotnet test $Project --no-restore --verbosity minimal --logger "console;verbosity=normal"
}

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Tests completed successfully!" -ForegroundColor Green
} else {
    Write-Host "❌ Tests failed!" -ForegroundColor Red
}