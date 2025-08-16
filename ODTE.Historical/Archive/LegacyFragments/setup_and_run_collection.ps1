# ODTE 20-Year Data Collection Setup and Execution Script
# This script helps set up API keys and run the comprehensive data collection

param(
    [string]$Mode = "test",
    [switch]$SetupKeys,
    [switch]$Help
)

Write-Host "🚀 ODTE 20-Year Historical Data Collection" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

if ($Help) {
    Write-Host ""
    Write-Host "Usage:" -ForegroundColor Yellow
    Write-Host "  .\setup_and_run_collection.ps1 [options]" -ForegroundColor White
    Write-Host ""
    Write-Host "Options:" -ForegroundColor Yellow
    Write-Host "  -Mode <mode>    Collection mode (test|full|resume|validate|optimize)" -ForegroundColor White
    Write-Host "  -SetupKeys      Interactive API key setup" -ForegroundColor White
    Write-Host "  -Help           Show this help message" -ForegroundColor White
    Write-Host ""
    Write-Host "Modes:" -ForegroundColor Yellow
    Write-Host "  test      - Test with recent 30 days (recommended first)" -ForegroundColor White
    Write-Host "  full      - Complete 20-year collection (2005-2025)" -ForegroundColor White  
    Write-Host "  resume    - Resume from previous progress" -ForegroundColor White
    Write-Host "  validate  - Validate existing data" -ForegroundColor White
    Write-Host "  optimize  - Optimize database only" -ForegroundColor White
    Write-Host ""
    Write-Host "Examples:" -ForegroundColor Yellow
    Write-Host "  .\setup_and_run_collection.ps1 -SetupKeys" -ForegroundColor Green
    Write-Host "  .\setup_and_run_collection.ps1 -Mode test" -ForegroundColor Green
    Write-Host "  .\setup_and_run_collection.ps1 -Mode full" -ForegroundColor Green
    exit
}

# Function to setup API keys
function Setup-ApiKeys {
    Write-Host ""
    Write-Host "🔑 API Key Setup" -ForegroundColor Yellow
    Write-Host "=================" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "You'll need API keys from these providers:" -ForegroundColor White
    Write-Host "1. Polygon.io (https://polygon.io/) - Premium data, 5 calls/min free" -ForegroundColor Gray
    Write-Host "2. Alpha Vantage (https://www.alphavantage.co/) - Free tier available" -ForegroundColor Gray  
    Write-Host "3. Twelve Data (https://twelvedata.com/) - Free tier available" -ForegroundColor Gray
    Write-Host ""
    
    # Check existing keys
    $polygonKey = [Environment]::GetEnvironmentVariable("POLYGON_API_KEY", "User")
    $alphaKey = [Environment]::GetEnvironmentVariable("ALPHA_VANTAGE_API_KEY", "User")
    $twelveKey = [Environment]::GetEnvironmentVariable("TWELVE_DATA_API_KEY", "User")
    
    Write-Host "Current API Keys Status:" -ForegroundColor White
    Write-Host "  Polygon.io:     $(if ($polygonKey) { '✅ Set' } else { '❌ Not Set' })" -ForegroundColor $(if ($polygonKey) { 'Green' } else { 'Red' })
    Write-Host "  Alpha Vantage:  $(if ($alphaKey) { '✅ Set' } else { '❌ Not Set' })" -ForegroundColor $(if ($alphaKey) { 'Green' } else { 'Red' })
    Write-Host "  Twelve Data:    $(if ($twelveKey) { '✅ Set' } else { '❌ Not Set' })" -ForegroundColor $(if ($twelveKey) { 'Green' } else { 'Red' })
    Write-Host ""
    
    # Prompt for missing keys
    if (-not $polygonKey) {
        $newPolygonKey = Read-Host "Enter Polygon.io API key (or press Enter to skip)"
        if ($newPolygonKey) {
            [Environment]::SetEnvironmentVariable("POLYGON_API_KEY", $newPolygonKey, "User")
            Write-Host "✅ Polygon.io API key saved" -ForegroundColor Green
        }
    }
    
    if (-not $alphaKey) {
        $newAlphaKey = Read-Host "Enter Alpha Vantage API key (or press Enter to skip)"
        if ($newAlphaKey) {
            [Environment]::SetEnvironmentVariable("ALPHA_VANTAGE_API_KEY", $newAlphaKey, "User")
            Write-Host "✅ Alpha Vantage API key saved" -ForegroundColor Green
        }
    }
    
    if (-not $twelveKey) {
        $newTwelveKey = Read-Host "Enter Twelve Data API key (or press Enter to skip)"
        if ($newTwelveKey) {
            [Environment]::SetEnvironmentVariable("TWELVE_DATA_API_KEY", $newTwelveKey, "User")
            Write-Host "✅ Twelve Data API key saved" -ForegroundColor Green
        }
    }
    
    Write-Host ""
    Write-Host "⚠️  Note: You may need to restart your terminal/IDE for new environment variables to take effect" -ForegroundColor Yellow
    Write-Host ""
}

# Function to check prerequisites
function Check-Prerequisites {
    Write-Host "🔍 Checking Prerequisites..." -ForegroundColor Yellow
    
    # Check .NET
    try {
        $dotnetVersion = dotnet --version
        Write-Host "✅ .NET SDK: $dotnetVersion" -ForegroundColor Green
    } catch {
        Write-Host "❌ .NET SDK not found. Please install .NET 9.0 SDK" -ForegroundColor Red
        return $false
    }
    
    # Check project files
    $projectFile = "C:\code\ODTE\ODTE.Historical\ODTE.Historical.csproj"
    if (Test-Path $projectFile) {
        Write-Host "✅ ODTE.Historical project found" -ForegroundColor Green
    } else {
        Write-Host "❌ ODTE.Historical project not found at $projectFile" -ForegroundColor Red
        return $false
    }
    
    # Check data directory
    $dataDir = "C:\code\ODTE\Data"
    if (-not (Test-Path $dataDir)) {
        Write-Host "📁 Creating data directory: $dataDir" -ForegroundColor Yellow
        New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
    }
    Write-Host "✅ Data directory ready" -ForegroundColor Green
    
    # Check API keys
    $hasAnyKey = [Environment]::GetEnvironmentVariable("POLYGON_API_KEY", "User") -or
                 [Environment]::GetEnvironmentVariable("ALPHA_VANTAGE_API_KEY", "User") -or  
                 [Environment]::GetEnvironmentVariable("TWELVE_DATA_API_KEY", "User")
    
    if ($hasAnyKey) {
        Write-Host "✅ At least one API key configured" -ForegroundColor Green
    } else {
        Write-Host "⚠️  No API keys found - will run with mock data only" -ForegroundColor Yellow
        Write-Host "   Run with -SetupKeys to configure API access" -ForegroundColor Gray
    }
    
    return $true
}

# Function to estimate collection time and data size
function Show-CollectionEstimate {
    param([string]$mode)
    
    Write-Host ""
    Write-Host "📊 Collection Estimate for Mode: $mode" -ForegroundColor Yellow
    Write-Host "====================================" -ForegroundColor Yellow
    
    switch ($mode) {
        "test" {
            Write-Host "⏱️  Estimated Time: 2-5 minutes" -ForegroundColor Green
            Write-Host "💾 Estimated Size: 1-5 MB" -ForegroundColor Green
            Write-Host "🔌 API Calls: ~50-100" -ForegroundColor Green
            Write-Host "📈 Scope: 2 symbols, 30 days" -ForegroundColor Gray
        }
        "full" {
            Write-Host "⏱️  Estimated Time: 6-12 hours" -ForegroundColor Red
            Write-Host "💾 Estimated Size: 20-50 GB" -ForegroundColor Red
            Write-Host "🔌 API Calls: ~100,000-500,000" -ForegroundColor Red
            Write-Host "📈 Scope: 25+ symbols, 20 years" -ForegroundColor Gray
            Write-Host ""
            Write-Host "⚠️  WARNING: This is a major operation!" -ForegroundColor Red
            Write-Host "   - Ensure stable internet connection" -ForegroundColor Yellow
            Write-Host "   - Have sufficient API quota" -ForegroundColor Yellow
            Write-Host "   - Free disk space (50GB+)" -ForegroundColor Yellow
            Write-Host "   - Consider running overnight" -ForegroundColor Yellow
        }
        "resume" {
            Write-Host "⏱️  Estimated Time: Variable (depends on progress)" -ForegroundColor Yellow
            Write-Host "💾 Estimated Size: Incremental" -ForegroundColor Yellow
            Write-Host "📈 Scope: Remaining incomplete data" -ForegroundColor Gray
        }
        "validate" {
            Write-Host "⏱️  Estimated Time: 5-15 minutes" -ForegroundColor Green
            Write-Host "💾 Estimated Size: No additional data" -ForegroundColor Green
            Write-Host "🔍 Scope: Quality analysis of existing data" -ForegroundColor Gray
        }
        "optimize" {
            Write-Host "⏱️  Estimated Time: 1-5 minutes" -ForegroundColor Green
            Write-Host "💾 Size Change: May increase slightly (indexes)" -ForegroundColor Yellow
            Write-Host "⚡ Purpose: Query performance optimization" -ForegroundColor Gray
        }
    }
    Write-Host ""
}

# Main execution
if ($SetupKeys) {
    Setup-ApiKeys
    exit
}

# Check prerequisites
if (-not (Check-Prerequisites)) {
    Write-Host ""
    Write-Host "❌ Prerequisites check failed. Please address the issues above." -ForegroundColor Red
    exit 1
}

# Show estimates
Show-CollectionEstimate $Mode

# Confirmation for full mode
if ($Mode -eq "full") {
    Write-Host ""
    $confirm = Read-Host "Are you sure you want to start the full 20-year collection? (y/N)"
    if ($confirm.ToLower() -notin @("y", "yes")) {
        Write-Host "Collection cancelled." -ForegroundColor Yellow
        exit
    }
}

# Build the project
Write-Host "🔨 Building ODTE.Historical..." -ForegroundColor Yellow
try {
    Push-Location "C:\code\ODTE\ODTE.Historical"
    $buildResult = dotnet build --configuration Release 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Build failed:" -ForegroundColor Red
        Write-Host $buildResult -ForegroundColor Red
        exit 1
    }
    Write-Host "✅ Build successful" -ForegroundColor Green
} finally {
    Pop-Location
}

# Run the data collection
Write-Host ""
Write-Host "🚀 Starting Data Collection (Mode: $Mode)..." -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

try {
    Push-Location "C:\code\ODTE\ODTE.Historical"
    
    # Start with timestamp
    $startTime = Get-Date
    Write-Host "📅 Started at: $($startTime.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor Gray
    Write-Host ""
    
    # Execute the program
    dotnet run --configuration Release -- $Mode
    
    $endTime = Get-Date
    $duration = $endTime - $startTime
    
    Write-Host ""
    Write-Host "✅ Data collection completed!" -ForegroundColor Green
    Write-Host "📅 Finished at: $($endTime.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor Gray
    Write-Host "⏱️  Total duration: $($duration.ToString('hh\:mm\:ss'))" -ForegroundColor Gray
    
    # Show database stats if available
    $dbPath = "C:\code\ODTE\Data\ODTE_TimeSeries_20Y.db"
    if (Test-Path $dbPath) {
        $dbSize = (Get-Item $dbPath).Length / 1MB
        Write-Host "💾 Database size: $($dbSize.ToString('N1')) MB" -ForegroundColor Gray
    }
    
} catch {
    Write-Host "❌ Error during execution: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
} finally {
    Pop-Location
}

Write-Host ""
Write-Host "🎉 All done! Your database is ready for ODTE backtesting." -ForegroundColor Green

# Suggest next steps
Write-Host ""
Write-Host "💡 Next Steps:" -ForegroundColor Yellow
Write-Host "  1. Run validation: .\setup_and_run_collection.ps1 -Mode validate" -ForegroundColor White
Write-Host "  2. Optimize for queries: .\setup_and_run_collection.ps1 -Mode optimize" -ForegroundColor White
Write-Host "  3. Start backtesting with your ODTE strategies!" -ForegroundColor White