-- Schema inspection for PM212 database
.headers on
.mode markdown

-- List all tables
SELECT name AS table_name, type FROM sqlite_schema WHERE type IN ('table','view') ORDER BY 1;

-- Get schema for each table
.schema

-- Inspect trades table specifically if it exists
PRAGMA table_info(trades);

-- Check if PM212_Trades exists instead
PRAGMA table_info(PM212_Trades);

-- Show sample data
SELECT sql FROM sqlite_schema WHERE type='table' AND name LIKE '%trade%';
SELECT sql FROM sqlite_schema WHERE type='table' AND name LIKE '%PM212%';