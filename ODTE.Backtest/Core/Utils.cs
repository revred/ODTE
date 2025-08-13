namespace ODTE.Backtest.Core;

/// <summary>
/// Utility functions for common operations in the backtest engine.
/// WHY: Centralized helper functions reduce code duplication and provide consistent behavior.
/// Following ConvertStar patterns: small, focused utility functions with clear responsibilities.
/// </summary>
public static class Utils
{
    /// <summary>
    /// Clamp a value between minimum and maximum bounds.
    /// USAGE: Enforce limits on ratios, percentages, and risk parameters.
    /// Example: Clamp(0.35, 0.0, 1.0) returns 0.35 (within bounds)
    ///          Clamp(1.5, 0.0, 1.0) returns 1.0 (clamped to max)
    /// </summary>
    public static double Clamp(this double v, double lo, double hi) 
        => Math.Max(lo, Math.Min(hi, v));
    
    /// <summary>
    /// Explicitly mark DateTime as UTC to avoid timezone confusion.
    /// WHY: Financial data often comes without timezone info, but our logic assumes UTC.
    /// CRITICAL: Mismatched timezones can cause off-by-hours errors in market sessions.
    /// </summary>
    public static DateTime ToUtc(this DateTime dt)
        => DateTime.SpecifyKind(dt, DateTimeKind.Utc);
    
    /// <summary>
    /// Check if timestamp falls within Regular Trading Hours (RTH).
    /// RTH = 9:30 AM - 4:00 PM Eastern Time
    /// 
    /// WHY RTH-ONLY TRADING:
    /// - Higher liquidity and tighter spreads
    /// - Avoids overnight gaps and low-volume periods
    /// - Most retail options activity occurs during RTH
    /// - Matches institutional trading patterns
    /// 
    /// TIMEZONE HANDLING:
    /// WARNING: Simplified approach subtracts 5 hours (EST). 
    /// PRODUCTION: Use proper timezone libraries (NodaTime) for DST handling.
    /// 
    /// MARKET HOURS REFERENCE:
    /// - Pre-market: 4:00 AM - 9:30 AM ET
    /// - Regular: 9:30 AM - 4:00 PM ET  ← WE TRADE HERE
    /// - After-hours: 4:00 PM - 8:00 PM ET
    /// </summary>
    public static bool IsRth(this DateTime tsUtc)
    {
        var et = tsUtc.AddHours(-5);  // UTC to EST (ignores DST)
        var t = et.TimeOfDay;
        return t >= new TimeSpan(9,30,0) && t <= new TimeSpan(16,0,0);
    }
    
    /// <summary>
    /// Get market session start time (9:30 AM ET = 2:30 PM UTC).
    /// Used for:
    /// - Opening Range calculations (first 15 minutes)
    /// - Session-relative time calculations
    /// - Day-of-week filtering
    /// 
    /// NOTE: Assumes EST (UTC-5). In production, handle DST properly.
    /// </summary>
    public static DateTime SessionStart(this DateTime tsUtc) 
        => new DateTime(tsUtc.Year, tsUtc.Month, tsUtc.Day, 14, 30, 0, DateTimeKind.Utc); 
    
    /// <summary>
    /// Get market session end time (4:00 PM ET = 9:00 PM UTC).
    /// Used for:
    /// - "No new risk" window (final 40 minutes before close)
    /// - PM cash settlement of index options
    /// - End-of-day position cleanup
    /// 
    /// SPX/XSP SETTLEMENT:
    /// - AM settlement: Based on opening prices (SPX only)
    /// - PM settlement: Based on closing prices (SPXW, XSP) ← WE USE THIS
    /// Reference: https://www.cboe.com/tradable_products/sp_500/spx_options/specifications/
    /// </summary>
    public static DateTime SessionEnd(this DateTime tsUtc)   
        => new DateTime(tsUtc.Year, tsUtc.Month, tsUtc.Day, 21, 0, 0, DateTimeKind.Utc);
}