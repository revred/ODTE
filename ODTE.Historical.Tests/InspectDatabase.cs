using Dapper;
using System.Data.SQLite;

namespace ODTE.Historical.Tests
{
    public static class DatabaseInspector
    {
        public static async Task InspectDatabaseAsync(string databasePath)
        {
            Console.WriteLine($"🔍 Inspecting database: {databasePath}");
            Console.WriteLine();

            using var conn = new SQLiteConnection($"Data Source={databasePath}");
            await conn.OpenAsync();

            // Get all tables
            var tables = await conn.QueryAsync<string>(@"
                SELECT name FROM sqlite_master 
                WHERE type='table' 
                ORDER BY name");

            Console.WriteLine("📋 Tables found:");
            foreach (var table in tables)
            {
                Console.WriteLine($"  • {table}");

                // Get column info
                var columns = await conn.QueryAsync(@$"PRAGMA table_info({table})");
                foreach (var col in columns)
                {
                    Console.WriteLine($"    - {col.name} ({col.type})");
                }

                // Get row count
                var count = await conn.QuerySingleAsync<int>($"SELECT COUNT(*) FROM {table}");
                Console.WriteLine($"    Rows: {count:N0}");
                Console.WriteLine();
            }
        }
    }
}