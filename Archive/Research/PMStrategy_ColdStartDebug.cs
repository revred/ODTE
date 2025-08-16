using System;
using System.Threading.Tasks;
using Xunit;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// Debug cold start functionality
    /// </summary>
    public class PMStrategy_ColdStartDebug
    {
        [Fact]
        public async Task Debug_ColdStart_PM250_Simple()
        {
            // Arrange
            var loader = new PMStrategy_ColdStartLoader();
            
            Console.WriteLine("🔍 DEBUGGING COLD START");
            
            // Act
            try
            {
                var result = await loader.ExecuteComprehensiveTesting("PM250", 2020, new[] { 3 });
                
                // Debug output
                Console.WriteLine($"Result ToolVersion: '{result.ToolVersion}'");
                Console.WriteLine($"Result Year: {result.Year}");
                Console.WriteLine($"Result TotalTrades: {result.TotalTrades}");
                Console.WriteLine($"Result TradeLedger Count: {result.TradeLedger.Count}");
                
                Assert.NotNull(result);
                Assert.NotEmpty(result.ToolVersion);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw;
            }
        }
    }
}