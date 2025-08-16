@echo off
echo 🚀 ODTE AUTHENTIC MARKET DATA ACQUISITION
echo ===========================================
echo.
echo This script will download 20 years of authentic market data (2005-present)
echo from Yahoo Finance and convert it to SQLite for the ODTE system.
echo.
echo ESTIMATED REQUIREMENTS:
echo - Time: 2-4 hours (depending on internet speed)
echo - Data: ~1-2 GB download, ~100 MB final SQLite database  
echo - Internet: Stable connection required
echo.
echo WARNING: This will make thousands of API calls to Yahoo Finance.
echo The script includes rate limiting to be respectful to their servers.
echo.
set /p continue="Continue with data acquisition? (y/N): "
if /i not "%continue%"=="y" (
    echo Acquisition cancelled.
    pause
    exit /b
)

echo.
echo 📦 Starting chunked data acquisition...
echo.

cd /d "C:\code\ODTE\ODTE.Strategy.Tests"
dotnet test --filter "Execute_Complete_Data_Acquisition_2005_To_Present" --logger "console;verbosity=detailed"

echo.
echo ✅ Data acquisition complete!
echo Check the acquisition report in the staging directory for details.
echo.
echo 🎯 NEXT STEPS:
echo 1. Run PM250 genetic algorithm tests with new historical data
echo 2. Validate strategy performance across the full 20-year period  
echo 3. Update production deployment with enhanced data coverage
echo.
pause