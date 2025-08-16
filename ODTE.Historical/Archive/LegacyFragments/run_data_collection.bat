@echo off
REM ODTE 20-Year Data Collection - Simple Batch Runner
REM This provides a simple interface to run the data collection

title ODTE Historical Data Collection

echo.
echo 🚀 ODTE 20-Year Historical Data Collection
echo =========================================
echo.

REM Check if .NET is available
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo ❌ .NET SDK not found. Please install .NET 9.0 SDK
    echo    Download from: https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

REM Show menu
:menu
echo Select collection mode:
echo.
echo 1. Test Collection     (30 days, 2 symbols - recommended first)
echo 2. Full Collection     (20 years, all symbols - WARNING: takes hours)
echo 3. Resume Collection   (continue from previous progress)
echo 4. Validate Data       (check existing data quality)
echo 5. Optimize Database   (optimize for faster queries)
echo 6. Setup API Keys      (configure data provider access)
echo 7. Exit
echo.

set /p choice="Enter your choice (1-7): "

if "%choice%"=="1" goto test
if "%choice%"=="2" goto full
if "%choice%"=="3" goto resume
if "%choice%"=="4" goto validate
if "%choice%"=="5" goto optimize
if "%choice%"=="6" goto setup_keys
if "%choice%"=="7" goto exit
echo Invalid choice. Please try again.
goto menu

:test
echo.
echo 🧪 Starting Test Collection (30 days)...
echo This will take 2-5 minutes and use minimal API quota
echo.
goto run_collection

:full
echo.
echo ⚠️  FULL 20-YEAR COLLECTION WARNING ⚠️
echo.
echo This operation will:
echo - Take 6-12 hours to complete
echo - Use 100,000+ API calls
echo - Create ~50GB database
echo - Require stable internet connection
echo.
set /p confirm="Are you sure? Type 'YES' to continue: "
if not "%confirm%"=="YES" (
    echo Collection cancelled.
    goto menu
)
echo.
echo 🚀 Starting Full Collection (2005-2025)...
echo.
set "mode=full"
goto run_collection

:resume
echo.
echo 🔄 Resuming Previous Collection...
echo.
set "mode=resume"
goto run_collection

:validate
echo.
echo 🔍 Validating Existing Data...
echo.
set "mode=validate"
goto run_collection

:optimize
echo.
echo ⚡ Optimizing Database...
echo.
set "mode=optimize"
goto run_collection

:setup_keys
echo.
echo 🔑 API Key Setup
echo ================
echo.
echo You need API keys from these providers:
echo 1. Polygon.io (https://polygon.io/)
echo 2. Alpha Vantage (https://www.alphavantage.co/)
echo 3. Twelve Data (https://twelvedata.com/)
echo.
echo Current API Keys Status:
if defined POLYGON_API_KEY (echo   Polygon.io: ✅ Set) else (echo   Polygon.io: ❌ Not Set)
if defined ALPHA_VANTAGE_API_KEY (echo   Alpha Vantage: ✅ Set) else (echo   Alpha Vantage: ❌ Not Set)
if defined TWELVE_DATA_API_KEY (echo   Twelve Data: ✅ Set) else (echo   Twelve Data: ❌ Not Set)
echo.

if not defined POLYGON_API_KEY (
    set /p polygon_key="Enter Polygon.io API key (or press Enter to skip): "
    if defined polygon_key (
        setx POLYGON_API_KEY "!polygon_key!" >nul
        echo ✅ Polygon.io API key saved
    )
)

if not defined ALPHA_VANTAGE_API_KEY (
    set /p alpha_key="Enter Alpha Vantage API key (or press Enter to skip): "
    if defined alpha_key (
        setx ALPHA_VANTAGE_API_KEY "!alpha_key!" >nul
        echo ✅ Alpha Vantage API key saved
    )
)

if not defined TWELVE_DATA_API_KEY (
    set /p twelve_key="Enter Twelve Data API key (or press Enter to skip): "
    if defined twelve_key (
        setx TWELVE_DATA_API_KEY "!twelve_key!" >nul
        echo ✅ Twelve Data API key saved
    )
)

echo.
echo ⚠️  Note: You may need to restart this script for new API keys to take effect
echo.
pause
goto menu

:run_collection
REM Default to test mode if not set
if not defined mode set "mode=test"

REM Create data directory if it doesn't exist
if not exist "C:\code\ODTE\Data" mkdir "C:\code\ODTE\Data"

REM Build the project
echo 🔨 Building project...
cd /d "C:\code\ODTE\ODTE.Historical"
dotnet build --configuration Release >nul 2>&1
if errorlevel 1 (
    echo ❌ Build failed. Check your project setup.
    pause
    goto menu
)

echo ✅ Build successful
echo.

REM Record start time
echo 📅 Started at: %date% %time%
echo.

REM Run the collection
dotnet run --configuration Release -- %mode%

REM Show completion
echo.
echo 📅 Finished at: %date% %time%

REM Show database size if it exists
if exist "C:\code\ODTE\Data\ODTE_TimeSeries_20Y.db" (
    echo 💾 Database created successfully
) else (
    echo ⚠️  Database not found - check for errors above
)

echo.
echo 🎉 Collection completed!
echo.
pause
goto menu

:exit
echo.
echo Thanks for using ODTE Historical Data Collection!
pause
exit /b 0