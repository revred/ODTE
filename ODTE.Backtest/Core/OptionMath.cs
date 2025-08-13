using System;

namespace ODTE.Backtest.Core;

/// <summary>
/// Black-Scholes option pricing and Greeks calculations.
/// WHY: We need Delta & fair-value pricing to synthesize quotes when vendor data isn't available.
/// 
/// LIMITATIONS:
/// - Assumes European options (appropriate for SPX/XSP index options)
/// - Constant volatility and risk-free rate
/// - Continuous dividend yield
/// - No early exercise (European style)
/// 
/// GOOD ENOUGH FOR: Gate entries, estimate fair values, synthetic quote generation
/// NOT FOR: Production pricing, exact P&L calculations, complex volatility models
/// 
/// BLACK-SCHOLES MODEL EXPLAINED:
/// C = S₀·e^(-q·T)·N(d₁) - K·e^(-r·T)·N(d₂)  [Call price]
/// P = K·e^(-r·T)·N(-d₂) - S₀·e^(-q·T)·N(-d₁) [Put price]
/// 
/// Where:
/// d₁ = [ln(S₀/K) + (r - q + σ²/2)·T] / (σ·√T)
/// d₂ = d₁ - σ·√T
/// 
/// PARAMETERS:
/// S₀ = Current stock price
/// K = Strike price
/// r = Risk-free rate
/// q = Dividend yield
/// σ = Volatility (sigma)
/// T = Time to expiration (years)
/// N(x) = Cumulative normal distribution
/// 
/// References:
/// - Black-Scholes Model: https://en.wikipedia.org/wiki/Black%E2%80%93Scholes_model
/// - Greeks Explanation: https://www.investopedia.com/terms/g/greeks.asp
/// - Options Theory: Hull's "Options, Futures, and Other Derivatives"
/// </summary>
public static class OptionMath
{
    /// <summary>
    /// Calculate d1 parameter for Black-Scholes formula.
    /// d1 represents the standardized moneyness adjusted for time and volatility.
    /// Used in both pricing and Delta calculations.
    /// </summary>
    public static double D1(double S, double K, double r, double q, double sigma, double T)
        => (Math.Log(S/K) + (r - q + 0.5*sigma*sigma)*T) / (sigma*Math.Sqrt(T));
    
    /// <summary>
    /// Calculate d2 parameter for Black-Scholes formula.
    /// d2 = d1 - σ√T, represents risk-adjusted probability of exercise.
    /// </summary>
    public static double D2(double d1, double sigma, double T) 
        => d1 - sigma*Math.Sqrt(T);

    /// <summary>
    /// Cumulative standard normal distribution N(x).
    /// Probability that a standard normal random variable is ≤ x.
    /// Used to calculate probabilities in Black-Scholes formula.
    /// </summary>
    public static double Nd(double x) 
        => 0.5 * (1.0 + Erf(x / Math.Sqrt(2.0))); 
    
    /// <summary>
    /// Standard normal probability density function n(x).
    /// Currently unused but useful for calculating other Greeks (Gamma, Vega).
    /// </summary>
    public static double nd(double x) 
        => Math.Exp(-0.5*x*x) / Math.Sqrt(2*Math.PI); 

    /// <summary>
    /// Calculate option Delta - sensitivity to underlying price movement.
    /// 
    /// DELTA INTERPRETATION:
    /// - Call Delta: 0 to 1 (positive exposure to underlying)
    /// - Put Delta: -1 to 0 (negative exposure to underlying)
    /// - 0.30 Delta ≈ 30% probability of finishing in-the-money
    /// - Higher absolute delta = more sensitive to price movement
    /// 
    /// STRATEGY USAGE:
    /// - Filter strikes by delta bands (7-15 delta for condors, 10-20 for singles)
    /// - Exit when short strike delta exceeds threshold (gamma risk protection)
    /// - Delta-neutral strategies maintain near-zero portfolio delta
    /// 
    /// EDGE CASES:
    /// - If T ≤ 0: Option expired, return 0
    /// - If σ ≤ 0: No volatility, return intrinsic delta
    /// </summary>
    public static double Delta(double S, double K, double r, double q, double sigma, double T, Right right)
    {
        if (T <= 0 || sigma <= 0) return 0;
        var d1 = D1(S,K,r,q,sigma,T);
        return right == Right.Call 
            ? Math.Exp(-q*T)*Nd(d1)      // Call delta: always positive
            : -Math.Exp(-q*T)*Nd(-d1);   // Put delta: always negative
    }

    /// <summary>
    /// Calculate theoretical option price using Black-Scholes formula.
    /// 
    /// PRICING BREAKDOWN:
    /// Call = [Stock × Prob(exercise)] - [Strike × Discount × Prob(assignment)]
    /// Put = [Strike × Discount × Prob(assignment)] - [Stock × Prob(exercise)]
    /// 
    /// REAL-WORLD ADJUSTMENTS NEEDED:
    /// - Volatility smile: IV varies by strike and expiration
    /// - American vs European exercise features
    /// - Interest rate curves vs flat rate
    /// - Dividend schedules vs continuous yield
    /// 
    /// PROTOTYPE USE:
    /// Generate synthetic quotes when real market data unavailable.
    /// Apply skew (puts cost more than calls for equity indices).
    /// 
    /// EDGE CASES:
    /// - Expired options (T ≤ 0): Return intrinsic value
    /// - Zero volatility: Return max(intrinsic, 0)
    /// </summary>
    public static double Price(double S, double K, double r, double q, double sigma, double T, Right right)
    {
        // Handle expiration: return intrinsic value
        if (T <= 0 || sigma <= 0) 
            return Math.Max(0, right == Right.Call ? S - K : K - S);
        
        var d1 = D1(S,K,r,q,sigma,T); 
        var d2 = D2(d1, sigma, T);
        
        if (right == Right.Call)
            // Call = S·e^(-q·T)·N(d1) - K·e^(-r·T)·N(d2)
            return Math.Exp(-q*T)*S*Nd(d1) - Math.Exp(-r*T)*K*Nd(d2);
        else
            // Put = K·e^(-r·T)·N(-d2) - S·e^(-q·T)·N(-d1)
            return Math.Exp(-r*T)*K*Nd(-d2) - Math.Exp(-q*T)*S*Nd(-d1);
    }

    /// <summary>
    /// Numerical approximation of the error function erf(x).
    /// WHY: .NET doesn't include erf(), but we need it for normal distribution.
    /// 
    /// ACCURACY: Good to ~1.5×10^-7 (sufficient for option pricing)
    /// ALGORITHM: Abramowitz and Stegun approximation
    /// 
    /// RELATIONSHIP TO NORMAL DISTRIBUTION:
    /// N(x) = 0.5 × [1 + erf(x/√2)]
    /// 
    /// FOR PRODUCTION: Consider using more precise implementations or Math.NET
    /// Reference: "Handbook of Mathematical Functions" - Abramowitz & Stegun
    /// </summary>
    private static double Erf(double x)
    {
        double t = 1.0/(1.0+0.5*Math.Abs(x));
        double tau = t*Math.Exp(-x*x - 1.26551223 + t*(1.00002368 + t*(0.37409196 + t*(0.09678418 + 
            t*(-0.18628806 + t*(0.27886807 + t*(-1.13520398 + t*(1.48851587 + t*(-0.82215223 + t*0.17087277)))))))));
        return x>=0 ? 1.0 - tau : tau - 1.0;
    }
}