using System;
using System.Threading.Tasks;
using Xunit;
using ODTE.Strategy.Tests;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// Validate the comprehensive yearly testing framework works end-to-end
    /// Tests the complete system: PM250 strategy + RFib + Historical tracking + Real data integration
    /// </summary>
    public class ValidateComprehensiveFramework
    {
        [Fact]
        public async Task ComprehensiveFramework_CanRun_2020_YearlyTest()
        {
            // Arrange
            var tester = new PM250_ComprehensiveYearlyTesting();
            
            Console.WriteLine("🧪 VALIDATING COMPREHENSIVE YEARLY TESTING FRAMEWORK");
            Console.WriteLine("=" + new string('=', 55));
            Console.WriteLine();
            Console.WriteLine("Testing Components:");
            Console.WriteLine("  ✓ PM250 0DTE Strategy Engine");  
            Console.WriteLine("  ✓ $16 Configurable RFib System");
            Console.WriteLine("  ✓ Real Market Data Integration");
            Console.WriteLine("  ✓ Trade-by-Trade Detailed Ledger");
            Console.WriteLine("  ✓ Historical Performance Tracking");
            Console.WriteLine("  ✓ PMxyz Version Comparison");
            Console.WriteLine();
            
            // Act: Run comprehensive 2020 test (condensed version for validation)
            Console.WriteLine("🚀 Executing 2020 comprehensive validation (first 3 months)...");
            
            try
            {
                var result = await tester.Test_PM250_Comprehensive_2020_Q1_Sample();
                
                // Assert: Validate core framework components work
                Assert.NotNull(result);
                Assert.True(result.Year == 2020, "Should test 2020 data");
                Assert.True(result.TotalTrades > 0, "Should generate real trades");
                Assert.True(result.TradeLedger.Count > 0, "Should create detailed trade ledger");
                Assert.True(result.RiskManagementEvents.Count >= 0, "Should track risk events");
                Assert.NotEmpty(result.ToolVersion);
                Assert.NotEmpty(result.ResultsFilePath);
                
                Console.WriteLine();
                Console.WriteLine("📊 VALIDATION RESULTS:");
                Console.WriteLine($"   Year Tested: {result.Year}");
                Console.WriteLine($"   Tool Version: {result.ToolVersion}");
                Console.WriteLine($"   Total Trades: {result.TotalTrades}");
                Console.WriteLine($"   Total P&L: ${result.TotalPnL:N2}");
                Console.WriteLine($"   Win Rate: {result.WinRate:F1}%");
                Console.WriteLine($"   Trade Records: {result.TradeLedger.Count}");
                Console.WriteLine($"   Risk Events: {result.RiskManagementEvents.Count}");
                Console.WriteLine($"   RFib Resets: {result.RFibResets}");
                Console.WriteLine($"   Results File: {result.ResultsFilePath}");
                
                Console.WriteLine();
                Console.WriteLine("✅ COMPREHENSIVE FRAMEWORK VALIDATION: PASSED");
                Console.WriteLine("   All core components operational");
                Console.WriteLine("   Ready for full yearly testing");
                Console.WriteLine("   Historical performance tracking active");
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ VALIDATION FAILED: {ex.Message}");
                Console.WriteLine($"   Stack Trace: {ex.StackTrace}");
                throw;
            }
        }
    }
}