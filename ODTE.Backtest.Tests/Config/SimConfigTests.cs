using FluentAssertions;
using ODTE.Backtest.Config;
using Xunit;

namespace ODTE.Backtest.Tests.Config;

/// <summary>
/// Comprehensive tests for configuration classes and their validation logic.
/// Tests default values, business rule constraints, and configuration consistency.
/// </summary>
public class SimConfigTests
{
    [Fact]
    public void SimConfig_DefaultConstructor_ShouldHaveReasonableDefaults()
    {
        // Act
        var config = new SimConfig();

        // Assert
        config.Underlying.Should().Be("XSP");
        config.Mode.Should().Be("prototype");
        config.RthOnly.Should().BeTrue();
        config.Timezone.Should().Be("Europe/London");
        config.CadenceSeconds.Should().Be(900); // 15 minutes
        config.NoNewRiskMinutesToClose.Should().Be(40);

        // Nested configs should be initialized
        config.ShortDelta.Should().NotBeNull();
        config.WidthPoints.Should().NotBeNull();
        config.CreditPerWidthMin.Should().NotBeNull();
        config.Stops.Should().NotBeNull();
        config.Risk.Should().NotBeNull();
        config.Slippage.Should().NotBeNull();
        config.Fees.Should().NotBeNull();
        config.Signals.Should().NotBeNull();
        config.Throttle.Should().NotBeNull();
        config.Paths.Should().NotBeNull();
    }

    [Fact]
    public void SimConfig_Properties_ShouldBeSettable()
    {
        // Arrange
        var config = new SimConfig();
        var startDate = DateOnly.FromDateTime(new DateTime(2024, 1, 1));
        var endDate = DateOnly.FromDateTime(new DateTime(2024, 12, 31));

        // Act
        config.Underlying = "SPX";
        config.Start = startDate;
        config.End = endDate;
        config.Mode = "production";
        config.RthOnly = false;
        config.Timezone = "America/New_York";
        config.CadenceSeconds = 300; // 5 minutes
        config.NoNewRiskMinutesToClose = 60;

        // Assert
        config.Underlying.Should().Be("SPX");
        config.Start.Should().Be(startDate);
        config.End.Should().Be(endDate);
        config.Mode.Should().Be("production");
        config.RthOnly.Should().BeFalse();
        config.Timezone.Should().Be("America/New_York");
        config.CadenceSeconds.Should().Be(300);
        config.NoNewRiskMinutesToClose.Should().Be(60);
    }

    [Fact]
    public void ShortDeltaCfg_DefaultValues_ShouldFollowBestPractices()
    {
        // Act
        var deltaConfig = new ShortDeltaCfg();

        // Assert
        deltaConfig.CondorMin.Should().Be(0.07); // 7 delta
        deltaConfig.CondorMax.Should().Be(0.15); // 15 delta
        deltaConfig.SingleMin.Should().Be(0.10); // 10 delta
        deltaConfig.SingleMax.Should().Be(0.20); // 20 delta

        // Business rules validation
        deltaConfig.CondorMin.Should().BeLessThan(deltaConfig.CondorMax);
        deltaConfig.SingleMin.Should().BeLessThan(deltaConfig.SingleMax);
        deltaConfig.CondorMin.Should().BeInRange(0.05, 0.25); // Reasonable range for short deltas
        deltaConfig.SingleMax.Should().BeInRange(0.15, 0.30); // Conservative for 0DTE
    }

    [Theory]
    [InlineData(0.05, 0.10, 0.08, 0.15)] // Valid configuration
    [InlineData(0.07, 0.15, 0.10, 0.20)] // Default configuration
    public void ShortDeltaCfg_ValidRanges_ShouldAcceptReasonableValues(
        double condorMin, double condorMax, double singleMin, double singleMax)
    {
        // Arrange & Act
        var deltaConfig = new ShortDeltaCfg
        {
            CondorMin = condorMin,
            CondorMax = condorMax,
            SingleMin = singleMin,
            SingleMax = singleMax
        };

        // Assert
        deltaConfig.CondorMin.Should().BeLessThan(deltaConfig.CondorMax);
        deltaConfig.SingleMin.Should().BeLessThan(deltaConfig.SingleMax);
        
        // All values should be positive and reasonable for options
        new[] { condorMin, condorMax, singleMin, singleMax }
            .Should().AllSatisfy(d => d.Should().BeInRange(0.01, 0.50));
    }

    [Fact]
    public void WidthPointsCfg_DefaultValues_ShouldBeConservativeForXSP()
    {
        // Act
        var widthConfig = new WidthPointsCfg();

        // Assert
        widthConfig.Min.Should().Be(1); // $100 max loss for XSP
        widthConfig.Max.Should().Be(2); // $200 max loss for XSP
        widthConfig.Min.Should().BeLessThanOrEqualTo(widthConfig.Max);
    }

    [Theory]
    [InlineData(1, 2)] // Default
    [InlineData(1, 3)] // Slightly wider
    [InlineData(2, 5)] // Wider for larger accounts
    public void WidthPointsCfg_ValidRanges_ShouldAcceptReasonableValues(int min, int max)
    {
        // Arrange & Act
        var widthConfig = new WidthPointsCfg
        {
            Min = min,
            Max = max
        };

        // Assert
        widthConfig.Min.Should().BeLessThanOrEqualTo(widthConfig.Max);
        widthConfig.Min.Should().BeGreaterThan(0);
        widthConfig.Max.Should().BeLessThanOrEqualTo(10); // Reasonable upper bound
    }

    [Fact]
    public void CreditPerWidthCfg_DefaultValues_ShouldAvoidPenniesInFrontOfSteamroller()
    {
        // Act
        var creditConfig = new CreditPerWidthCfg();

        // Assert
        creditConfig.Condor.Should().Be(0.18); // 18% minimum
        creditConfig.Single.Should().Be(0.20); // 20% minimum (higher for directional risk)
        
        // Condor can accept slightly lower credit due to two-sided nature
        creditConfig.Single.Should().BeGreaterThanOrEqualTo(creditConfig.Condor);
        
        // Both should be reasonable minimums
        creditConfig.Condor.Should().BeInRange(0.10, 0.30);
        creditConfig.Single.Should().BeInRange(0.15, 0.35);
    }

    [Theory]
    [InlineData(0.15, 0.20)] // Conservative
    [InlineData(0.18, 0.22)] // Default-ish
    [InlineData(0.25, 0.30)] // Aggressive (higher credit requirements)
    public void CreditPerWidthCfg_ValidRanges_ShouldAcceptReasonableRatios(double condor, double single)
    {
        // Arrange & Act
        var creditConfig = new CreditPerWidthCfg
        {
            Condor = condor,
            Single = single
        };

        // Assert
        creditConfig.Condor.Should().BeInRange(0.05, 0.50); // 5% to 50% seems reasonable
        creditConfig.Single.Should().BeInRange(0.05, 0.50);
        creditConfig.Single.Should().BeGreaterThanOrEqualTo(creditConfig.Condor * 0.8); // Allow some flexibility
    }

    [Fact]
    public void StopsCfg_DefaultValues_ShouldProtectAgainstGammaRisk()
    {
        // Act
        var stopsConfig = new StopsCfg();

        // Assert
        stopsConfig.CreditMultiple.Should().Be(2.2); // Exit at 2.2x credit loss
        stopsConfig.DeltaBreach.Should().Be(0.33); // Exit at 33 delta (gamma danger zone)
        
        // Business rules
        stopsConfig.CreditMultiple.Should().BeInRange(1.5, 4.0); // Reasonable stop loss range
        stopsConfig.DeltaBreach.Should().BeInRange(0.25, 0.45); // Before significant gamma acceleration
    }

    [Theory]
    [InlineData(2.0, 0.30)] // Tight stops
    [InlineData(2.2, 0.33)] // Default
    [InlineData(2.5, 0.35)] // Looser stops
    public void StopsCfg_ValidRanges_ShouldAcceptReasonableStopLevels(double creditMultiple, double deltaBreach)
    {
        // Arrange & Act
        var stopsConfig = new StopsCfg
        {
            CreditMultiple = creditMultiple,
            DeltaBreach = deltaBreach
        };

        // Assert
        stopsConfig.CreditMultiple.Should().BeInRange(1.2, 5.0);
        stopsConfig.DeltaBreach.Should().BeInRange(0.20, 0.50);
    }

    [Fact]
    public void RiskCfg_DefaultValues_ShouldProtectCapital()
    {
        // Act
        var riskConfig = new RiskCfg();

        // Assert
        riskConfig.DailyLossStop.Should().Be(500); // $500 daily loss limit
        riskConfig.PerTradeMaxLossCap.Should().Be(200); // $200 per trade max loss
        riskConfig.MaxConcurrentPerSide.Should().Be(2); // Max 2 positions per side
        
        // Business rules
        riskConfig.DailyLossStop.Should().BeGreaterThan(riskConfig.PerTradeMaxLossCap);
        riskConfig.MaxConcurrentPerSide.Should().BeInRange(1, 10);
    }

    [Fact]
    public void SlippageCfg_DefaultValues_ShouldReflectRealityOfExecution()
    {
        // Act
        var slippageConfig = new SlippageCfg();

        // Assert
        slippageConfig.EntryHalfSpreadTicks.Should().Be(0.5);
        slippageConfig.ExitHalfSpreadTicks.Should().Be(0.5);
        slippageConfig.LateSessionExtraTicks.Should().Be(0.5);
        slippageConfig.TickValue.Should().Be(0.05); // $0.05 for index options
        slippageConfig.SpreadPctCap.Should().Be(0.25); // 25% max spread
        
        // Business rules
        slippageConfig.TickValue.Should().BeGreaterThan(0);
        slippageConfig.SpreadPctCap.Should().BeInRange(0.10, 0.50);
        slippageConfig.LateSessionExtraTicks.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void FeesCfg_DefaultValues_ShouldReflectTypicalRetailCosts()
    {
        // Act
        var feesConfig = new FeesCfg();

        // Assert
        feesConfig.CommissionPerContract.Should().Be(0.65);
        feesConfig.ExchangeFeesPerContract.Should().Be(0.25);
        
        // Total cost per contract
        var totalCostPerContract = feesConfig.CommissionPerContract + feesConfig.ExchangeFeesPerContract;
        totalCostPerContract.Should().Be(0.90);
        
        // Total cost per spread (2 contracts)
        var totalCostPerSpread = totalCostPerContract * 2;
        totalCostPerSpread.Should().Be(1.80);
    }

    [Fact]
    public void SignalsCfg_DefaultValues_ShouldFollowTechnicalAnalysisBestPractices()
    {
        // Act
        var signalsConfig = new SignalsCfg();

        // Assert
        signalsConfig.OrMinutes.Should().Be(15); // 15-minute opening range
        signalsConfig.VwapWindowMinutes.Should().Be(30); // 30-minute VWAP window
        signalsConfig.AtrPeriodBars.Should().Be(20); // 20-period ATR
        signalsConfig.CalmIvCondition.Should().Be("short_leq_30d");
        signalsConfig.EventBlockMinutesBefore.Should().Be(60); // 1 hour before events
        signalsConfig.EventBlockMinutesAfter.Should().Be(15); // 15 minutes after events
        
        // Business rules
        signalsConfig.OrMinutes.Should().BeInRange(5, 30);
        signalsConfig.VwapWindowMinutes.Should().BeInRange(15, 120);
        signalsConfig.AtrPeriodBars.Should().BeInRange(10, 50);
        signalsConfig.EventBlockMinutesBefore.Should().BeGreaterThan(signalsConfig.EventBlockMinutesAfter);
    }

    [Fact]
    public void ThrottleCfg_DefaultValues_ShouldAdaptToMarketConditions()
    {
        // Act
        var throttleConfig = new ThrottleCfg();

        // Assert
        throttleConfig.RvHighCadenceSeconds.Should().Be(1800); // 30 minutes when volatile
        throttleConfig.RvLowCadenceSeconds.Should().Be(600); // 10 minutes when calm
        
        // Business rules
        throttleConfig.RvHighCadenceSeconds.Should().BeGreaterThan(throttleConfig.RvLowCadenceSeconds);
        throttleConfig.RvLowCadenceSeconds.Should().BeInRange(300, 1800); // 5-30 minutes
        throttleConfig.RvHighCadenceSeconds.Should().BeInRange(900, 3600); // 15-60 minutes
    }

    [Fact]
    public void PathsCfg_DefaultValues_ShouldPointToSampleFiles()
    {
        // Act
        var pathsConfig = new PathsCfg();

        // Assert
        pathsConfig.BarsCsv.Should().Be("./Samples/bars_spx_min.csv");
        pathsConfig.VixCsv.Should().Be("./Samples/vix_daily.csv");
        pathsConfig.Vix9dCsv.Should().Be("./Samples/vix9d_daily.csv");
        pathsConfig.CalendarCsv.Should().Be("./Samples/calendar.csv");
        pathsConfig.ReportsDir.Should().Be("./Reports");
        
        // All paths should be non-empty
        new[] { pathsConfig.BarsCsv, pathsConfig.VixCsv, pathsConfig.Vix9dCsv, 
                pathsConfig.CalendarCsv, pathsConfig.ReportsDir }
            .Should().AllSatisfy(path => path.Should().NotBeNullOrEmpty());
    }

    [Fact]
    public void ConfigurationConsistency_ShouldHaveLogicalRelationships()
    {
        // Arrange
        var config = new SimConfig();

        // Act & Assert - Business rule consistency checks
        
        // Delta ranges should be logical
        config.ShortDelta.CondorMin.Should().BeLessThan(config.ShortDelta.CondorMax);
        config.ShortDelta.SingleMin.Should().BeLessThan(config.ShortDelta.SingleMax);
        
        // Width ranges should be logical
        config.WidthPoints.Min.Should().BeLessThanOrEqualTo(config.WidthPoints.Max);
        
        // Risk management should be consistent
        config.Risk.DailyLossStop.Should().BeGreaterThan(config.Risk.PerTradeMaxLossCap);
        
        // Throttling should make sense
        config.Throttle.RvHighCadenceSeconds.Should().BeGreaterThan(config.Throttle.RvLowCadenceSeconds);
        
        // Event blocking should be logical
        config.Signals.EventBlockMinutesBefore.Should().BeGreaterThan(config.Signals.EventBlockMinutesAfter);
        
        // Credit requirements should be reasonable
        config.CreditPerWidthMin.Single.Should().BeInRange(0.1, 0.5);
        config.CreditPerWidthMin.Condor.Should().BeInRange(0.1, 0.5);
    }

    [Theory]
    [InlineData("XSP")]
    [InlineData("SPX")]
    [InlineData("SPY")]
    [InlineData("QQQ")]
    public void SimConfig_CommonUnderlyings_ShouldBeSupported(string underlying)
    {
        // Arrange & Act
        var config = new SimConfig { Underlying = underlying };

        // Assert
        config.Underlying.Should().Be(underlying);
        config.Underlying.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("prototype")]
    [InlineData("production")]
    [InlineData("backtest")]
    public void SimConfig_ValidModes_ShouldBeAccepted(string mode)
    {
        // Arrange & Act
        var config = new SimConfig { Mode = mode };

        // Assert
        config.Mode.Should().Be(mode);
    }

    [Theory]
    [InlineData("America/New_York")]
    [InlineData("Europe/London")]
    [InlineData("UTC")]
    [InlineData("America/Chicago")]
    public void SimConfig_CommonTimezones_ShouldBeSupported(string timezone)
    {
        // Arrange & Act
        var config = new SimConfig { Timezone = timezone };

        // Assert
        config.Timezone.Should().Be(timezone);
    }

    [Fact]
    public void CompleteConfiguration_ShouldPassRealisticValidation()
    {
        // Arrange & Act - Create a realistic configuration
        var config = new SimConfig
        {
            Underlying = "XSP",
            Start = DateOnly.FromDateTime(new DateTime(2024, 1, 1)),
            End = DateOnly.FromDateTime(new DateTime(2024, 12, 31)),
            Mode = "prototype",
            RthOnly = true,
            Timezone = "America/New_York",
            CadenceSeconds = 900,
            NoNewRiskMinutesToClose = 40,
            
            ShortDelta = new ShortDeltaCfg
            {
                CondorMin = 0.07,
                CondorMax = 0.15,
                SingleMin = 0.10,
                SingleMax = 0.20
            },
            
            Risk = new RiskCfg
            {
                DailyLossStop = 500,
                PerTradeMaxLossCap = 200,
                MaxConcurrentPerSide = 2
            }
        };

        // Assert - All critical relationships should be valid
        config.Start.Should().BeBefore(config.End);
        config.ShortDelta.CondorMin.Should().BeLessThan(config.ShortDelta.CondorMax);
        config.Risk.DailyLossStop.Should().BeGreaterThan(config.Risk.PerTradeMaxLossCap);
        config.CadenceSeconds.Should().BeGreaterThan(0);
        config.NoNewRiskMinutesToClose.Should().BeInRange(0, 120);
    }
}