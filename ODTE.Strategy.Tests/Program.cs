using System;

namespace ODTE.Strategy.Tests;

/// <summary>
/// Centralized entry point for all ODTE Strategy test runners.
/// Usage: dotnet run [command] [args...]
/// Commands:
///   multileg    - Run multi-leg strategies validation
///   optimized   - Run PM250 optimized system validation  
///   genetic     - Run radical genetic breakthrough optimizer
///   ultra       - Run ultra-optimized implementation test
///   help        - Show this help message
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            ShowHelp();
            return;
        }

        var command = args[0].ToLowerInvariant();
        var remainingArgs = args.Length > 1 ? args[1..] : Array.Empty<string>();

        try
        {
            switch (command)
            {
                case "multileg":
                    Console.WriteLine("🎯 Running Multi-Leg Strategies Validation...\n");
                    MultiLegStrategiesValidation.RunValidation(remainingArgs);
                    break;

                case "optimized":
                    Console.WriteLine("🧬 Running PM250 Optimized System Validation...\n");
                    PM250_OptimizedSystem_ValidationTest.RunValidation(remainingArgs);
                    break;

                case "genetic":
                    Console.WriteLine("🧬 Running Radical Genetic Breakthrough...\n");
                    PM250_Radical_Genetic_Breakthrough.RunOptimization(remainingArgs);
                    break;

                case "ultra":
                    Console.WriteLine("🧬 Running Ultra-Optimized Implementation Test...\n");
                    PM250_UltraOptimized_ImplementationTest.RunTest(remainingArgs);
                    break;

                case "help":
                case "-h":
                case "--help":
                    ShowHelp();
                    break;

                default:
                    Console.WriteLine($"❌ Unknown command: {command}");
                    Console.WriteLine("Use 'dotnet run help' to see available commands.\n");
                    ShowHelp();
                    Environment.Exit(1);
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error executing command '{command}': {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static void ShowHelp()
    {
        Console.WriteLine("🎯 ODTE Strategy Test Runner");
        Console.WriteLine("============================");
        Console.WriteLine("Usage: dotnet run [command] [args...]");
        Console.WriteLine();
        Console.WriteLine("Available Commands:");
        Console.WriteLine("  multileg    - Run multi-leg strategies validation");
        Console.WriteLine("  optimized   - Run PM250 optimized system validation");
        Console.WriteLine("  genetic     - Run radical genetic breakthrough optimizer");
        Console.WriteLine("  ultra       - Run ultra-optimized implementation test");
        Console.WriteLine("  help        - Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet run multileg");
        Console.WriteLine("  dotnet run optimized");
        Console.WriteLine("  dotnet run genetic");
        Console.WriteLine("  dotnet run help");
    }
}