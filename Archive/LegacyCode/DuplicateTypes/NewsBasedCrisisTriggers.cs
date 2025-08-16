using System;
using System.Collections.Generic;
using System.Linq;

namespace ODTE.Strategy;

/// <summary>
/// News-Based Crisis Trigger Detection
/// 
/// PURPOSE: Detect high-impact news events that trigger market volatility
/// HISTORICAL IMPACT EVENTS:
/// - Fed Rate Decisions: 50+ bp moves cause 2-5% market swings
/// - Earnings Surprises: AAPL, MSFT, TSLA can move market 1-3%
/// - Geopolitical Events: War, sanctions, trade disputes
/// - Economic Data: CPI, Jobs, GDP surprises >0.3% from consensus
/// - Central Bank Communications: Hawkish/Dovish surprises
/// 
/// STRATEGY IMPACT:
/// - Pre-event: Reduce position sizes, increase hedging
/// - During event: Activate volatility expansion strategies
/// - Post-event: Capture mean reversion or momentum continuation
/// </summary>
public class NewsBasedCrisisTriggers
{
    private readonly List<NewsEvent> _recentEvents;
    private readonly Dictionary<EventType, EventImpactProfile> _impactProfiles;
    
    public NewsBasedCrisisTriggers()
    {
        _recentEvents = new List<NewsEvent>();
        _impactProfiles = InitializeImpactProfiles();
    }
    
    /// <summary>
    /// Analyze news events and determine market impact potential
    /// </summary>
    public NewsImpactAssessment AssessNewsImpact(DateTime currentDate, List<NewsEvent> todaysEvents)
    {
        // Update recent events
        foreach (var newsEvent in todaysEvents)
        {
            _recentEvents.Add(newsEvent);
        }
        
        // Clean old events (keep last 10 days)
        _recentEvents.RemoveAll(e => (currentDate - e.EventDate).TotalDays > 10);
        
        // Analyze today's events
        var highImpactEvents = todaysEvents.Where(e => IsHighImpactEvent(e)).ToList();
        var mediumImpactEvents = todaysEvents.Where(e => IsMediumImpactEvent(e)).ToList();
        
        // Calculate aggregate impact score
        var totalImpactScore = CalculateTotalImpactScore(todaysEvents);
        var crisisProbability = CalculateCrisisProbability(todaysEvents, totalImpactScore);
        
        // Determine recommended actions
        var assessment = new NewsImpactAssessment
        {
            Date = currentDate,
            TotalEvents = todaysEvents.Count,
            HighImpactEvents = highImpactEvents,
            MediumImpactEvents = mediumImpactEvents,
            ImpactScore = totalImpactScore,
            CrisisProbability = crisisProbability,
            RecommendedAction = GetRecommendedAction(totalImpactScore, crisisProbability),
            PositionSizeAdjustment = GetPositionSizeAdjustment(totalImpactScore),
            ConfidenceOverride = GetConfidenceOverride(totalImpactScore),
            VolatilityExpectation = GetVolatilityExpectation(todaysEvents)
        };
        
        if (highImpactEvents.Any() || totalImpactScore > 6)
        {
            Console.WriteLine($"📰 NEWS IMPACT ALERT: {assessment.ImpactScore:F1} score");
            Console.WriteLine($"   High Impact Events: {highImpactEvents.Count} | Crisis Probability: {crisisProbability:P0}");
            Console.WriteLine($"   Action: {assessment.RecommendedAction}");
        }
        
        return assessment;
    }
    
    /// <summary>
    /// Simulate realistic news events for battle testing
    /// </summary>
    public List<NewsEvent> GenerateRealisticNewsEvents(DateTime date, BattleTestPeriod period)
    {
        var events = new List<NewsEvent>();
        var dayNumber = (date - period.StartDate).Days + 1;
        
        // Generate period-specific events based on historical patterns
        events.AddRange(period.Name switch
        {
            "COVID-19 Crash" => GenerateCovidNewsEvents(dayNumber),
            "China Devaluation Crash" => GenerateChinaDevaluationEvents(dayNumber),
            "December 2018 Fed Panic" => GenerateFedPanicEvents(dayNumber),
            "October 2018 Vol Spike" => GenerateVolSpikeEvents(dayNumber),
            "September 2022 CPI Shock" => GenerateCPIShockEvents(dayNumber),
            "Omicron + Fed Pivot Fear" => GenerateOmicronFedEvents(dayNumber),
            "2019 Trade War Escalation" => GenerateTradeWarEvents(dayNumber),
            _ => GenerateNormalMarketEvents(dayNumber)
        });
        
        return events;
    }
    
    /// <summary>
    /// Check if event is high impact (>5 impact score)
    /// </summary>
    private bool IsHighImpactEvent(NewsEvent newsEvent)
    {
        if (!_impactProfiles.ContainsKey(newsEvent.EventType))
            return false;
            
        var profile = _impactProfiles[newsEvent.EventType];
        return profile.BaseImpactScore >= 5 || newsEvent.Severity == EventSeverity.High;
    }
    
    /// <summary>
    /// Check if event is medium impact (3-5 impact score)
    /// </summary>
    private bool IsMediumImpactEvent(NewsEvent newsEvent)
    {
        if (!_impactProfiles.ContainsKey(newsEvent.EventType))
            return false;
            
        var profile = _impactProfiles[newsEvent.EventType];
        return profile.BaseImpactScore >= 3 && profile.BaseImpactScore < 5;
    }
    
    /// <summary>
    /// Calculate total impact score for all events
    /// </summary>
    private double CalculateTotalImpactScore(List<NewsEvent> events)
    {
        double totalScore = 0;
        
        foreach (var newsEvent in events)
        {
            if (_impactProfiles.ContainsKey(newsEvent.EventType))
            {
                var profile = _impactProfiles[newsEvent.EventType];
                var eventScore = profile.BaseImpactScore;
                
                // Adjust for event severity
                eventScore *= newsEvent.Severity switch
                {
                    EventSeverity.Low => 0.7,
                    EventSeverity.Medium => 1.0,
                    EventSeverity.High => 1.5,
                    _ => 1.0
                };
                
                totalScore += eventScore;
            }
        }
        
        return totalScore;
    }
    
    /// <summary>
    /// Calculate probability of market crisis from news events
    /// </summary>
    private double CalculateCrisisProbability(List<NewsEvent> events, double impactScore)
    {
        var baseProbability = Math.Min(0.8, impactScore / 10.0); // Max 80% probability
        
        // Increase probability for specific crisis-prone event combinations
        var hasFedEvent = events.Any(e => e.EventType == EventType.FedDecision);
        var hasGeopolitical = events.Any(e => e.EventType == EventType.GeopoliticalTension);
        var hasEconomicData = events.Any(e => e.EventType == EventType.EconomicData);
        
        if (hasFedEvent && hasEconomicData) baseProbability += 0.15;
        if (hasGeopolitical && events.Count > 2) baseProbability += 0.1;
        
        return Math.Min(0.9, baseProbability);
    }
    
    /// <summary>
    /// Get recommended action based on news impact
    /// </summary>
    private string GetRecommendedAction(double impactScore, double crisisProbability)
    {
        return impactScore switch
        {
            > 8 => "CRISIS IMMINENT: Activate Black Swan protocols immediately",
            > 6 => "HIGH ALERT: Switch to crisis mode, hedge all positions",
            > 4 => "ELEVATED RISK: Reduce position sizes, increase hedging",
            > 2 => "MODERATE RISK: Monitor closely, prepare for volatility",
            _ => "LOW RISK: Continue normal operations with standard monitoring"
        };
    }
    
    /// <summary>
    /// Get position size adjustment multiplier
    /// </summary>
    private decimal GetPositionSizeAdjustment(double impactScore)
    {
        return impactScore switch
        {
            > 8 => 0.3m,  // Reduce to 30% of normal size
            > 6 => 0.5m,  // Reduce to 50% of normal size
            > 4 => 0.7m,  // Reduce to 70% of normal size
            > 2 => 0.85m, // Reduce to 85% of normal size
            _ => 1.0m     // Normal position sizing
        };
    }
    
    /// <summary>
    /// Get confidence threshold override
    /// </summary>
    private decimal GetConfidenceOverride(double impactScore)
    {
        return impactScore switch
        {
            > 8 => 0.2m,  // Trade with 20%+ confidence
            > 6 => 0.3m,  // Trade with 30%+ confidence
            > 4 => 0.4m,  // Trade with 40%+ confidence
            > 2 => 0.5m,  // Trade with 50%+ confidence
            _ => 0.6m     // Normal 60% confidence threshold
        };
    }
    
    /// <summary>
    /// Get expected volatility impact
    /// </summary>
    private VolatilityExpectation GetVolatilityExpectation(List<NewsEvent> events)
    {
        var maxImpact = events.Any() ? events.Max(e => _impactProfiles.GetValueOrDefault(e.EventType)?.BaseImpactScore ?? 0) : 0;
        
        return maxImpact switch
        {
            > 8 => VolatilityExpectation.Extreme,
            > 6 => VolatilityExpectation.High,
            > 4 => VolatilityExpectation.Elevated,
            > 2 => VolatilityExpectation.Moderate,
            _ => VolatilityExpectation.Normal
        };
    }
    
    /// <summary>
    /// Initialize impact profiles for different event types
    /// </summary>
    private Dictionary<EventType, EventImpactProfile> InitializeImpactProfiles()
    {
        return new Dictionary<EventType, EventImpactProfile>
        {
            [EventType.FedDecision] = new EventImpactProfile { BaseImpactScore = 7, VolatilityIncrease = 3.5, Duration = TimeSpan.FromHours(24) },
            [EventType.EconomicData] = new EventImpactProfile { BaseImpactScore = 5, VolatilityIncrease = 2.0, Duration = TimeSpan.FromHours(6) },
            [EventType.EarningsSurprise] = new EventImpactProfile { BaseImpactScore = 4, VolatilityIncrease = 1.5, Duration = TimeSpan.FromHours(2) },
            [EventType.GeopoliticalTension] = new EventImpactProfile { BaseImpactScore = 6, VolatilityIncrease = 2.5, Duration = TimeSpan.FromDays(3) },
            [EventType.CentralBankSpeech] = new EventImpactProfile { BaseImpactScore = 3, VolatilityIncrease = 1.2, Duration = TimeSpan.FromHours(4) },
            [EventType.TradeDispute] = new EventImpactProfile { BaseImpactScore = 5, VolatilityIncrease = 2.0, Duration = TimeSpan.FromDays(2) },
            [EventType.HealthCrisis] = new EventImpactProfile { BaseImpactScore = 8, VolatilityIncrease = 4.0, Duration = TimeSpan.FromDays(7) },
            [EventType.RegulatoryChange] = new EventImpactProfile { BaseImpactScore = 3, VolatilityIncrease = 1.0, Duration = TimeSpan.FromDays(1) }
        };
    }
    
    /// <summary>
    /// Generate COVID-19 period news events
    /// </summary>
    private List<NewsEvent> GenerateCovidNewsEvents(int dayNumber)
    {
        var events = new List<NewsEvent>();
        
        switch (dayNumber)
        {
            case 1: events.Add(new NewsEvent { EventType = EventType.HealthCrisis, Severity = EventSeverity.Medium, Description = "WHO monitoring pneumonia outbreak" }); break;
            case 3: events.Add(new NewsEvent { EventType = EventType.HealthCrisis, Severity = EventSeverity.High, Description = "WHO declares global health emergency" }); break;
            case 9: events.Add(new NewsEvent { EventType = EventType.GeopoliticalTension, Severity = EventSeverity.High, Description = "Italy announces nationwide lockdown" }); break;
            case 11: events.Add(new NewsEvent { EventType = EventType.HealthCrisis, Severity = EventSeverity.High, Description = "WHO declares COVID-19 pandemic" }); break;
            case 15: events.Add(new NewsEvent { EventType = EventType.FedDecision, Severity = EventSeverity.High, Description = "Fed emergency 100bp rate cut" }); break;
            case 16: events.Add(new NewsEvent { EventType = EventType.EconomicData, Severity = EventSeverity.High, Description = "Circuit breakers triggered" }); break;
        }
        
        return events;
    }
    
    private List<NewsEvent> GenerateChinaDevaluationEvents(int dayNumber)
    {
        var events = new List<NewsEvent>();
        if (dayNumber == 1) events.Add(new NewsEvent { EventType = EventType.TradeDispute, Severity = EventSeverity.High, Description = "China devalues yuan 1.9%" });
        if (dayNumber == 2) events.Add(new NewsEvent { EventType = EventType.GeopoliticalTension, Severity = EventSeverity.Medium, Description = "Currency war fears escalate" });
        return events;
    }
    
    private List<NewsEvent> GenerateFedPanicEvents(int dayNumber)
    {
        var events = new List<NewsEvent>();
        if (dayNumber == 12) events.Add(new NewsEvent { EventType = EventType.FedDecision, Severity = EventSeverity.High, Description = "Fed raises rates 25bp despite market turmoil" });
        if (dayNumber == 18) events.Add(new NewsEvent { EventType = EventType.CentralBankSpeech, Severity = EventSeverity.Medium, Description = "Powell hints at more rate hikes" });
        return events;
    }
    
    private List<NewsEvent> GenerateVolSpikeEvents(int dayNumber)
    {
        var events = new List<NewsEvent>();
        if (dayNumber == 8) events.Add(new NewsEvent { EventType = EventType.EarningsSurprise, Severity = EventSeverity.Medium, Description = "Tech earnings disappoint" });
        if (dayNumber == 15) events.Add(new NewsEvent { EventType = EventType.EconomicData, Severity = EventSeverity.High, Description = "10-year yield spikes to 3.25%" });
        return events;
    }
    
    private List<NewsEvent> GenerateCPIShockEvents(int dayNumber)
    {
        var events = new List<NewsEvent>();
        if (dayNumber == 13) events.Add(new NewsEvent { EventType = EventType.EconomicData, Severity = EventSeverity.High, Description = "CPI 8.3% vs 8.1% expected" });
        return events;
    }
    
    private List<NewsEvent> GenerateOmicronFedEvents(int dayNumber)
    {
        var events = new List<NewsEvent>();
        if (dayNumber == 3) events.Add(new NewsEvent { EventType = EventType.HealthCrisis, Severity = EventSeverity.Medium, Description = "Omicron variant concerns peak" });
        if (dayNumber == 8) events.Add(new NewsEvent { EventType = EventType.FedDecision, Severity = EventSeverity.High, Description = "Fed signals faster taper" });
        return events;
    }
    
    private List<NewsEvent> GenerateTradeWarEvents(int dayNumber)
    {
        var events = new List<NewsEvent>();
        if (dayNumber == 1) events.Add(new NewsEvent { EventType = EventType.TradeDispute, Severity = EventSeverity.Medium, Description = "Trump announces new China tariffs" });
        if (dayNumber == 3) events.Add(new NewsEvent { EventType = EventType.GeopoliticalTension, Severity = EventSeverity.Medium, Description = "China retaliates with yuan devaluation" });
        return events;
    }
    
    private List<NewsEvent> GenerateNormalMarketEvents(int dayNumber)
    {
        // Minimal events during normal periods
        var events = new List<NewsEvent>();
        if (dayNumber % 7 == 0) events.Add(new NewsEvent { EventType = EventType.EconomicData, Severity = EventSeverity.Low, Description = "Weekly economic data release" });
        return events;
    }
}

// Supporting classes and enums
public class NewsEvent
{
    public EventType EventType { get; set; }
    public EventSeverity Severity { get; set; }
    public string Description { get; set; } = "";
    public DateTime EventDate { get; set; } = DateTime.Now;
}

public class EventImpactProfile
{
    public double BaseImpactScore { get; set; }
    public double VolatilityIncrease { get; set; }
    public TimeSpan Duration { get; set; }
}

public class NewsImpactAssessment
{
    public DateTime Date { get; set; }
    public int TotalEvents { get; set; }
    public List<NewsEvent> HighImpactEvents { get; set; } = new();
    public List<NewsEvent> MediumImpactEvents { get; set; } = new();
    public double ImpactScore { get; set; }
    public double CrisisProbability { get; set; }
    public string RecommendedAction { get; set; } = "";
    public decimal PositionSizeAdjustment { get; set; } = 1.0m;
    public decimal ConfidenceOverride { get; set; } = 0.6m;
    public VolatilityExpectation VolatilityExpectation { get; set; }
}

public enum EventType
{
    FedDecision,
    EconomicData,
    EarningsSurprise,
    GeopoliticalTension,
    CentralBankSpeech,
    TradeDispute,
    HealthCrisis,
    RegulatoryChange
}

public enum EventSeverity
{
    Low,
    Medium,
    High
}

public enum VolatilityExpectation
{
    Normal,
    Moderate,
    Elevated,
    High,
    Extreme
}