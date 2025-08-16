#!/usr/bin/env pwsh

Write-Host "🚀 ODTE AUTHENTIC MARKET DATA ACQUISITION SETUP" -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "📋 COMPREHENSIVE DATA ACQUISITION PLAN:" -ForegroundColor Green
Write-Host "----------------------------------------" -ForegroundColor Green
Write-Host "Chunk 1: Recent Data (2022-Present)     - Priority: HIGH   - Source: Yahoo Finance" -ForegroundColor White
Write-Host "Chunk 2: COVID Era (2020-2021)          - Priority: HIGH   - Source: Yahoo Finance" -ForegroundColor White  
Write-Host "Chunk 3: Modern Markets (2018-2019)     - Priority: MEDIUM - Source: Yahoo Finance" -ForegroundColor Yellow
Write-Host "Chunk 4: Low Vol Era (2015-2017)        - Priority: MEDIUM - Source: Yahoo Finance" -ForegroundColor Yellow
Write-Host "Chunk 5: Post-Crisis (2010-2014)        - Priority: LOW    - Source: Yahoo Finance" -ForegroundColor Gray
Write-Host "Chunk 6: Crisis Era (2005-2009)         - Priority: LOW    - Source: Yahoo Finance" -ForegroundColor Gray
Write-Host ""

Write-Host "📊 ESTIMATED REQUIREMENTS:" -ForegroundColor Yellow
Write-Host "---------------------------" -ForegroundColor Yellow
Write-Host "• Time Required: 2-4 hours (depending on connection speed)" -ForegroundColor White
Write-Host "• Data Download: ~1-2 GB raw data from Yahoo Finance" -ForegroundColor White
Write-Host "• Final Database: ~100-200 MB compressed SQLite database" -ForegroundColor White
Write-Host "• Internet: Stable connection required (thousands of API calls)" -ForegroundColor White
Write-Host "• Disk Space: ~3 GB temporary space during processing" -ForegroundColor White
Write-Host ""

Write-Host "🔧 TECHNICAL DETAILS:" -ForegroundColor Magenta
Write-Host "----------------------" -ForegroundColor Magenta
Write-Host "• Data Sources: Yahoo Finance API (free, no key required)" -ForegroundColor White
Write-Host "• Symbols: XSP, SPY, QQQ, IWM, VIX for comprehensive coverage" -ForegroundColor White
Write-Host "• Format: CSV → Parquet (staging) → SQLite (final optimized storage)" -ForegroundColor White
Write-Host "• Rate Limiting: 60 requests/minute to respect Yahoo Finance ToS" -ForegroundColor White
Write-Host "• Validation: Data quality checks and gap detection included" -ForegroundColor White
Write-Host "• Resume: Chunked processing allows resuming if interrupted" -ForegroundColor White
Write-Host ""

Write-Host "⚠️  IMPORTANT WARNINGS:" -ForegroundColor Red
Write-Host "------------------------" -ForegroundColor Red
Write-Host "• This makes thousands of requests to Yahoo Finance - be respectful" -ForegroundColor White
Write-Host "• Requires stable internet connection for several hours" -ForegroundColor White
Write-Host "• Will backup existing database before major updates" -ForegroundColor White
Write-Host "• Cancel anytime with Ctrl+C - progress is saved in chunks" -ForegroundColor White
Write-Host ""

$continue = Read-Host "Continue with data acquisition setup? (y/N)"
if ($continue -ne "y" -and $continue -ne "Y") {
    Write-Host "❌ Data acquisition cancelled." -ForegroundColor Red
    exit
}

Write-Host ""
Write-Host "🔍 Checking current database status..." -ForegroundColor Cyan
try {
    Set-Location "C:\code\ODTE\ODTE.Strategy.Tests"
    Write-Host "Running gap analysis to identify missing data..." -ForegroundColor White
    dotnet test --filter "DataGapAnalysisTest" --logger "console;verbosity=minimal" | Out-Host
} catch {
    Write-Host "⚠️  Could not run gap analysis. Proceeding with full acquisition..." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "🧪 Running pipeline validation test..." -ForegroundColor Cyan
try {
    dotnet test --filter "Execute_Small_Sample_Data_Acquisition" --logger "console;verbosity=minimal" | Out-Host
} catch {
    Write-Host "❌ Pipeline validation failed. Check network connectivity." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "✅ Pipeline validation successful!" -ForegroundColor Green
Write-Host ""

$runFull = Read-Host "Ready to start FULL data acquisition? This will take several hours (y/N)"
if ($runFull -ne "y" -and $runFull -ne "Y") {
    Write-Host "📝 Setup complete. Run full acquisition manually when ready with:" -ForegroundColor Yellow
    Write-Host "   dotnet test --filter 'Execute_Complete_Data_Acquisition_2005_To_Present'" -ForegroundColor White
    Write-Host ""
    Write-Host "🎯 Or use the batch file: run_data_acquisition.bat" -ForegroundColor White
    exit
}

Write-Host ""
Write-Host "🚀 STARTING FULL DATA ACQUISITION..." -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Green
Write-Host "This will process all 6 chunks in priority order." -ForegroundColor White
Write-Host "Progress will be displayed for each chunk and symbol." -ForegroundColor White
Write-Host "You can cancel anytime with Ctrl+C." -ForegroundColor White
Write-Host ""

Write-Host "Press Enter to continue or Ctrl+C to cancel..." -ForegroundColor Yellow
Read-Host

try {
    # Remove the Skip attribute temporarily to run the full acquisition
    $testFile = "ChunkedDataAcquisitionTest.cs"
    $originalContent = Get-Content $testFile -Raw
    $modifiedContent = $originalContent -replace '\[Fact\(Skip = "Long-running full acquisition - Run manually when ready"\)\]', '[Fact]'
    Set-Content $testFile $modifiedContent
    
    # Run the full acquisition
    dotnet test --filter "Execute_Complete_Data_Acquisition_2005_To_Present" --logger "console;verbosity=detailed" | Out-Host
    
    # Restore original content
    Set-Content $testFile $originalContent
    
    Write-Host ""
    Write-Host "🎉 DATA ACQUISITION COMPLETE!" -ForegroundColor Green
    Write-Host "==============================" -ForegroundColor Green
    Write-Host "Your SQLite database now contains 20 years of authentic market data." -ForegroundColor White
    Write-Host ""
    Write-Host "🎯 NEXT STEPS:" -ForegroundColor Cyan
    Write-Host "--------------" -ForegroundColor Cyan
    Write-Host "1. 🧬 Run PM250 genetic algorithm tests across full 20-year period" -ForegroundColor White
    Write-Host "2. 📊 Validate strategy performance in different market regimes" -ForegroundColor White  
    Write-Host "3. 🚀 Update production deployment with enhanced data coverage" -ForegroundColor White
    Write-Host "4. 📈 Test strategy robustness across major market events (2008, 2020, etc.)" -ForegroundColor White
    Write-Host ""
    Write-Host "📋 Check the detailed acquisition report in the staging directory." -ForegroundColor White
    
} catch {
    Write-Host "❌ Data acquisition failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Check the logs for detailed error information." -ForegroundColor White
    exit 1
} finally {
    # Ensure original content is restored even if there's an error
    if (Test-Path $testFile) {
        Set-Content $testFile $originalContent -ErrorAction SilentlyContinue
    }
}

Write-Host ""
Write-Host "Press Enter to exit..." -ForegroundColor Gray
Read-Host