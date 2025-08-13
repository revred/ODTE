using ODTE.Backtest.Core;
using ODTE.LiveTrading.Interfaces;
using System.Collections.Concurrent;
using System.Text.Json;

namespace ODTE.LiveTrading.Brokers;

/// <summary>
/// Mock implementation of Robinhood API for testing and development.
/// 
/// ROBINHOOD-SPECIFIC FEATURES:
/// - Commission-free options trading (but with wider spreads)
/// - Mobile-first interface simulation
/// - Pattern Day Trader restrictions (strict enforcement)
/// - Limited options approval levels
/// - Real-time notifications and social features
/// - Margin requirements and Gold membership benefits
/// 
/// LIMITATIONS SIMULATED:
/// - No complex multi-leg spreads (max 4 legs)
/// - Limited options expiration dates
/// - Simplified options chain (fewer strikes)
/// - PDT restrictions strictly enforced
/// - No extended hours options trading
/// 
/// ROBINHOOD SPECIFICS:
/// - Uses OAuth 2.0 authentication
/// - REST API with JSON responses
/// - Real-time WebSocket feeds for quotes
/// - Push notifications for fills and margin calls
/// - Social trading features (disabled in production)
/// 
/// PRODUCTION INTEGRATION:
/// - Use Robinhood unofficial API (community-maintained)
/// - Handle OAuth token refresh
/// - Implement rate limiting (strict)
/// - Add 2FA support
/// - Monitor for API changes (Robinhood updates frequently)
/// 
/// References:
/// - Robinhood Web API: https://robinhood.com/us/en/support/articles/360001226846/
/// - PDT Rules: https://robinhood.com/us/en/support/articles/360001227026/
/// - Options Trading: https://robinhood.com/us/en/support/articles/360001331403/
/// </summary>
public class RobinhoodMockBroker : IBroker
{
    private readonly Random _random = new(42);
    private readonly ConcurrentDictionary<string, LiveOrder> _orders = new();
    private readonly ConcurrentDictionary<string, LivePosition> _positions = new();
    private readonly HttpClient _httpClient = new(); // Mock HTTP client
    
    // Robinhood-specific state
    private bool _isConnected = false;
    private BrokerCredentials? _credentials;
    private AccountInfo? _accountInfo;
    private string _accessToken = string.Empty;
    private DateTime _tokenExpiry = DateTime.MinValue;
    private DateTime _lastHeartbeat = DateTime.UtcNow;
    
    // Market data state
    private readonly Dictionary<string, decimal> _spotPrices = new()
    {
        ["AAPL"] = 180.0m,
        ["TSLA"] = 250.0m,
        ["SPY"] = 490.0m,
        ["QQQ"] = 380.0m,
        ["IWM"] = 200.0m
    };
    
    private int _nextOrderId = 1;
    private readonly Timer _marketDataTimer;
    private readonly Timer _tokenRefreshTimer;

    public string BrokerName => "Robinhood (Mock)";
    public bool IsConnected => _isConnected && IsTokenValid();
    public DateTime LastHeartbeat => _lastHeartbeat;

    // Events
    public event EventHandler<PositionUpdateEventArgs>? PositionUpdated;
    public event EventHandler<OrderUpdateEventArgs>? OrderUpdated;
    public event EventHandler<MarketDataEventArgs>? MarketDataUpdated;
    public event EventHandler<ODTE.LiveTrading.Interfaces.ErrorEventArgs>? ErrorOccurred;

    public RobinhoodMockBroker()
    {
        // Market data updates every 10 seconds (Robinhood is less frequent than IBKR)
        _marketDataTimer = new Timer(SimulateMarketData, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
        
        // Token refresh every 30 minutes
        _tokenRefreshTimer = new Timer(RefreshToken, null, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));
    }

    public async Task<bool> ConnectAsync(BrokerCredentials credentials)
    {
        try
        {
            // Simulate OAuth authentication flow
            OnError("Initiating OAuth authentication with Robinhood...", severity: "INFO");
            await Task.Delay(1500); // Robinhood auth is slower
            
            if (string.IsNullOrWhiteSpace(credentials.Username) || 
                string.IsNullOrWhiteSpace(credentials.ApiSecret)) // Using ApiSecret as password
            {
                OnError("Invalid credentials - username and password required");
                return false;
            }

            // Simulate 2FA challenge (mock)
            if (!credentials.PaperTrading)
            {
                OnError("2FA required - checking authentication app...", severity: "INFO");
                await Task.Delay(2000);
            }

            _credentials = credentials;
            _accessToken = GenerateMockToken();
            _tokenExpiry = DateTime.UtcNow.AddHours(24);
            _isConnected = true;
            _lastHeartbeat = DateTime.UtcNow;

            // Initialize Robinhood-style account
            _accountInfo = CreateRobinhoodAccountInfo(credentials);
            
            OnError($"Connected to Robinhood - Account: {_accountInfo.AccountId}", severity: "INFO");
            
            // Send welcome notification (Robinhood style)
            await SendPushNotification("Welcome back! Ready to trade options? 🚀");
            
            return true;
        }
        catch (Exception ex)
        {
            OnError($"Robinhood connection failed: {ex.Message}", ex);
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        _isConnected = false;
        _credentials = null;
        _accountInfo = null;
        _accessToken = string.Empty;
        
        _positions.Clear();
        _orders.Clear();
        
        OnError("Disconnected from Robinhood", severity: "INFO");
        await Task.CompletedTask;
    }

    public async Task<AccountInfo> GetAccountInfoAsync()
    {
        EnsureConnected();
        
        // Simulate API call delay
        await Task.Delay(300);
        
        // Calculate total portfolio value including crypto (Robinhood feature)
        var totalPnL = _positions.Values.Sum(p => p.UnrealizedPnL);
        var cryptoValue = _random.Next(0, 1000); // Mock crypto holdings
        
        var updatedAccount = _accountInfo! with
        {
            NetLiquidationValue = _accountInfo.NetLiquidationValue + totalPnL + cryptoValue,
            AvailableFunds = Math.Max(0, _accountInfo.AvailableFunds + totalPnL * 0.5m), // Conservative
            Balances = new Dictionary<string, decimal>(_accountInfo.Balances)
            {
                ["Crypto"] = cryptoValue
            }
        };
        
        _accountInfo = updatedAccount;
        return _accountInfo;
    }

    public async Task<IEnumerable<OptionQuote>> GetOptionChainAsync(string underlying, DateTime expiry)
    {
        EnsureConnected();
        
        // Robinhood has limited options chains
        if (!IsOptionsTradingAllowed(underlying))
        {
            OnError($"Options trading not available for {underlying} on Robinhood");
            return Enumerable.Empty<OptionQuote>();
        }
        
        await Task.Delay(400); // Robinhood API is slower
        
        var spot = _spotPrices.GetValueOrDefault(underlying, 100m);
        var quotes = new List<OptionQuote>();
        var now = DateTime.UtcNow;
        var expiryDate = DateOnly.FromDateTime(expiry);
        
        // Generate simplified option chain (Robinhood has fewer strikes)
        var timeToExpiry = (expiry - now).TotalDays / 365.0;
        var volatility = GetRobinhoodImpliedVolatility(underlying);
        
        // Limited strike range (±15% vs IBKR's ±20%)
        var minStrike = spot * 0.85m;
        var maxStrike = spot * 1.15m;
        var strikeIncrement = GetStrikeIncrement(underlying);
        
        for (var strike = minStrike; strike <= maxStrike; strike += strikeIncrement)
        {
            var strikeDouble = (double)strike;
            var spotDouble = (double)spot;
            
            var callPrice = CalculateOptionPrice(spotDouble, strikeDouble, timeToExpiry, volatility, 0.05, Right.Call);
            var putPrice = CalculateOptionPrice(spotDouble, strikeDouble, timeToExpiry, volatility, 0.05, Right.Put);
            
            var callDelta = CalculateOptionDelta(spotDouble, strikeDouble, timeToExpiry, volatility, 0.05, Right.Call);
            var putDelta = CalculateOptionDelta(spotDouble, strikeDouble, timeToExpiry, volatility, 0.05, Right.Put);
            
            // Wider spreads than IBKR (commission-free but wider bid-ask)
            var callSpread = Math.Max(0.10, callPrice * 0.05); // Min $0.10 or 5% of mid
            var putSpread = Math.Max(0.10, putPrice * 0.05);
            
            quotes.Add(new OptionQuote(
                Ts: now,
                Expiry: expiryDate,
                Strike: strikeDouble,
                Right: Right.Call,
                Bid: Math.Max(0, callPrice - callSpread / 2),
                Ask: callPrice + callSpread / 2,
                Mid: callPrice,
                Delta: callDelta,
                Iv: volatility
            ));
            
            quotes.Add(new OptionQuote(
                Ts: now,
                Expiry: expiryDate,
                Strike: strikeDouble,
                Right: Right.Put,
                Bid: Math.Max(0, putPrice - putSpread / 2),
                Ask: putPrice + putSpread / 2,
                Mid: putPrice,
                Delta: putDelta,
                Iv: volatility
            ));
        }
        
        return quotes;
    }

    public async Task<double> GetSpotPriceAsync(string underlying)
    {
        EnsureConnected();
        await Task.Delay(100); // Robinhood real-time data delay
        
        return (double)_spotPrices.GetValueOrDefault(underlying, 100m);
    }

    public async Task<MarketStatus> GetMarketStatusAsync()
    {
        await Task.Delay(50);
        
        var now = DateTime.UtcNow;
        var easternTime = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));
        var hour = easternTime.Hour;
        var dayOfWeek = easternTime.DayOfWeek;
        
        var isMarketDay = dayOfWeek >= DayOfWeek.Monday && dayOfWeek <= DayOfWeek.Friday;
        var isRegularHours = hour >= 9 && hour < 16;
        var isExtendedHours = (hour >= 4 && hour < 9) || (hour >= 16 && hour < 20);
        
        return new MarketStatus
        {
            IsOpen = isMarketDay && (isRegularHours || isExtendedHours),
            Session = isMarketDay switch
            {
                _ when isRegularHours => "REGULAR",
                _ when isExtendedHours => hour < 9 ? "PRE" : "POST",
                _ => "CLOSED"
            },
            ProductStatus = new Dictionary<string, bool>
            {
                ["Stocks"] = isMarketDay && (isRegularHours || isExtendedHours),
                ["Options"] = isMarketDay && isRegularHours, // No extended hours options
                ["Crypto"] = true // 24/7 crypto trading
            }
        };
    }

    public async Task<OrderResult> SubmitOrderAsync(LiveOrder order)
    {
        EnsureConnected();
        
        try
        {
            // Robinhood-specific validations
            if (!await ValidateRobinhoodSpecificRules(order))
            {
                return new OrderResult
                {
                    Success = false,
                    Message = "Order violates Robinhood trading rules",
                    Status = OrderStatus.Rejected
                };
            }
            
            await Task.Delay(200); // Robinhood order processing
            
            var orderId = $"RH_{_nextOrderId++}";
            var submittedOrder = order with 
            { 
                OrderId = orderId, 
                Status = OrderStatus.Submitted 
            };
            
            _orders[orderId] = submittedOrder;
            
            // Send push notification (Robinhood style)
            await SendPushNotification($"📊 Your {order.Type} order has been submitted");
            
            // Faster execution than IBKR (but less reliable)
            _ = Task.Run(async () =>
            {
                await Task.Delay(500); // Quick fill simulation
                await ExecuteOrder(orderId);
            });
            
            OnOrderUpdate(submittedOrder, OrderStatus.Pending);
            
            return new OrderResult
            {
                Success = true,
                OrderId = orderId,
                Message = "Order submitted to Robinhood",
                Status = OrderStatus.Submitted
            };
        }
        catch (Exception ex)
        {
            OnError($"Robinhood order submission failed: {ex.Message}", ex);
            return new OrderResult
            {
                Success = false,
                Message = ex.Message,
                Status = OrderStatus.Rejected
            };
        }
    }

    public async Task<OrderResult> CancelOrderAsync(string orderId)
    {
        EnsureConnected();
        await Task.Delay(100);
        
        if (_orders.TryGetValue(orderId, out var order))
        {
            if (order.Status == OrderStatus.Submitted)
            {
                var cancelledOrder = order with { Status = OrderStatus.Cancelled };
                _orders[orderId] = cancelledOrder;
                
                await SendPushNotification($"❌ Your order has been cancelled");
                OnOrderUpdate(cancelledOrder, order.Status);
                
                return new OrderResult
                {
                    Success = true,
                    OrderId = orderId,
                    Message = "Order cancelled",
                    Status = OrderStatus.Cancelled
                };
            }
        }
        
        return new OrderResult
        {
            Success = false,
            Message = "Order not found or cannot be cancelled",
            Status = OrderStatus.Rejected
        };
    }

    public async Task<OrderResult> ModifyOrderAsync(string orderId, LiveOrder newOrder)
    {
        // Robinhood doesn't support order modification - must cancel and resubmit
        var cancelResult = await CancelOrderAsync(orderId);
        if (!cancelResult.Success)
            return cancelResult;
            
        await Task.Delay(100); // Brief delay between cancel and new order
        return await SubmitOrderAsync(newOrder);
    }

    public async Task<IEnumerable<LivePosition>> GetPositionsAsync()
    {
        EnsureConnected();
        await Task.Delay(150);
        
        return _positions.Values.Where(p => p.IsOpen);
    }

    public async Task<IEnumerable<LiveOrder>> GetOrdersAsync(OrderStatus? status = null)
    {
        EnsureConnected();
        await Task.Delay(100);
        
        var orders = _orders.Values.AsEnumerable();
        
        if (status.HasValue)
            orders = orders.Where(o => o.Status == status.Value);
            
        return orders.OrderByDescending(o => o.CreatedAt);
    }

    public async Task<LiveOrder?> GetOrderAsync(string orderId)
    {
        EnsureConnected();
        await Task.Delay(75);
        
        return _orders.GetValueOrDefault(orderId);
    }

    public async Task<RiskLimits> GetRiskLimitsAsync()
    {
        EnsureConnected();
        await Task.Delay(100);
        
        var isGoldMember = _credentials?.ExtraParams.GetValueOrDefault("gold_member") == "true";
        
        return new RiskLimits
        {
            MaxOrderValue = isGoldMember ? 50000m : 5000m,
            DailyLossLimit = (_accountInfo?.NetLiquidationValue ?? 10000m) * 0.10m, // 10% limit
            MaxPositions = isGoldMember ? 100 : 20,
            MaxDelta = 50,
            MaxGamma = 25,
            AllowNakedOptions = false, // Robinhood doesn't allow naked options
            ProductLimits = new Dictionary<string, decimal>
            {
                ["Options"] = isGoldMember ? 25000m : 5000m,
                ["Spreads"] = isGoldMember ? 10000m : 2000m,
                ["Stocks"] = isGoldMember ? 50000m : 10000m,
                ["Crypto"] = 10000m
            }
        };
    }

    public async Task<bool> ValidateOrderAsync(LiveOrder order)
    {
        EnsureConnected();
        
        // Basic validation
        if (order.Quantity <= 0 || string.IsNullOrWhiteSpace(order.Underlying))
            return false;
            
        // Check options approval
        if (order.Legs.Any() && _accountInfo?.MaxOptionsLevel < 2)
            return false;
            
        return await ValidateRobinhoodSpecificRules(order);
    }

    // Robinhood-specific helper methods
    private async Task<bool> ValidateRobinhoodSpecificRules(LiveOrder order)
    {
        // Pattern Day Trader check (strict enforcement)
        if (await IsDayTrade(order) && !_accountInfo!.PatternDayTrader)
        {
            var dayTradesToday = await CountDayTradesToday();
            if (dayTradesToday >= 3) // PDT rule violation
            {
                OnError("Pattern Day Trader violation - account restricted", severity: "WARNING");
                return false;
            }
        }
        
        // Multi-leg spread limitations (max 4 legs)
        if (order.Legs.Count > 4)
        {
            OnError("Robinhood supports maximum 4-leg spreads only");
            return false;
        }
        
        // No extended hours options
        var marketStatus = await GetMarketStatusAsync();
        if (order.Legs.Any() && marketStatus.Session != "REGULAR")
        {
            OnError("Options trading only during regular market hours");
            return false;
        }
        
        return true;
    }

    private async Task<bool> IsDayTrade(LiveOrder order)
    {
        // Check if this would be a day trade (simplified logic)
        var existingPosition = _positions.Values
            .FirstOrDefault(p => p.Underlying == order.Underlying && p.IsOpen);
            
        return existingPosition?.OpenedAt.Date == DateTime.Today;
    }

    private async Task<int> CountDayTradesToday()
    {
        // Count completed day trades today
        return _orders.Values
            .Where(o => o.FilledAt?.Date == DateTime.Today)
            .Count();
    }

    private bool IsTokenValid()
    {
        return !string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiry;
    }

    private string GenerateMockToken()
    {
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray())[..16];
    }

    private void RefreshToken(object? state)
    {
        if (_isConnected && DateTime.UtcNow.AddMinutes(5) >= _tokenExpiry)
        {
            _accessToken = GenerateMockToken();
            _tokenExpiry = DateTime.UtcNow.AddHours(24);
            OnError("OAuth token refreshed", severity: "INFO");
        }
    }

    private bool IsOptionsTradingAllowed(string underlying)
    {
        // Robinhood has limited options universe
        var allowedSymbols = new HashSet<string> 
        { 
            "AAPL", "TSLA", "SPY", "QQQ", "IWM", "MSFT", "NVDA", "AMZN", "GOOGL", "META" 
        };
        return allowedSymbols.Contains(underlying);
    }

    private decimal GetStrikeIncrement(string underlying)
    {
        return underlying switch
        {
            "SPY" or "QQQ" => 1.0m,   // $1 increments
            "AAPL" or "TSLA" => 2.5m, // $2.50 increments  
            _ => 5.0m                  // $5 increments for others
        };
    }

    private double GetRobinhoodImpliedVolatility(string underlying)
    {
        // Robinhood tends to show slightly different IVs due to different data sources
        return underlying switch
        {
            "AAPL" or "MSFT" => 0.25 + _random.NextDouble() * 0.10, // 25-35%
            "TSLA" or "NVDA" => 0.40 + _random.NextDouble() * 0.20, // 40-60%
            "SPY" or "QQQ" => 0.15 + _random.NextDouble() * 0.08,   // 15-23%
            _ => 0.30 + _random.NextDouble() * 0.15                  // 30-45%
        };
    }

    private AccountInfo CreateRobinhoodAccountInfo(BrokerCredentials credentials)
    {
        var isPaper = credentials.PaperTrading;
        var isGoldMember = credentials.ExtraParams.GetValueOrDefault("gold_member") == "true";
        var baseValue = isPaper ? 50000m : (isGoldMember ? 25000m : 10000m);
        
        return new AccountInfo
        {
            AccountId = $"RH{_random.Next(100000, 999999)}",
            NetLiquidationValue = baseValue,
            AvailableFunds = baseValue * 0.9m,
            BuyingPower = baseValue * (isGoldMember ? 2m : 1m), // Gold gets 2x margin
            MaintenanceMargin = 0m,
            OptionsLevel = true,
            MaxOptionsLevel = isGoldMember ? 3 : 2, // Gold gets higher level
            PatternDayTrader = baseValue >= 25000m,
            Balances = new Dictionary<string, decimal>
            {
                ["USD"] = baseValue,
                ["Stocks"] = 0m,
                ["Options"] = 0m,
                ["Crypto"] = isPaper ? 1000m : 0m
            }
        };
    }

    private async Task SendPushNotification(string message)
    {
        // Simulate Robinhood's push notification system
        OnError($"📱 {message}", severity: "INFO");
        await Task.CompletedTask;
    }

    private void SimulateMarketData(object? state)
    {
        if (!_isConnected) return;
        
        foreach (var (symbol, currentPrice) in _spotPrices.ToList())
        {
            // More volatile moves than IBKR (retail-focused)
            var change = (decimal)(_random.NextDouble() - 0.5) * currentPrice * 0.003m; // 0.3% moves
            var newPrice = Math.Max(1m, currentPrice + change);
            
            _spotPrices[symbol] = newPrice;
            OnMarketDataUpdate(symbol, newPrice, DateTime.UtcNow);
        }
        
        _lastHeartbeat = DateTime.UtcNow;
    }

    private async Task ExecuteOrder(string orderId)
    {
        if (!_orders.TryGetValue(orderId, out var order))
            return;
            
        try
        {
            // Robinhood execution with different characteristics
            var fillPrice = CalculateRobinhoodFillPrice(order);
            var filledOrder = order with 
            { 
                Status = OrderStatus.Filled, 
                FilledAt = DateTime.UtcNow,
                FilledPrice = fillPrice,
                FilledQuantity = order.Quantity
            };
            
            _orders[orderId] = filledOrder;
            
            var position = CreatePositionFromOrder(filledOrder);
            _positions[position.PositionId] = position;
            
            // Send fill notification
            await SendPushNotification($"✅ Your {order.Type} order has been filled at ${fillPrice:F2}");
            
            OnOrderUpdate(filledOrder, OrderStatus.Submitted);
            OnPositionUpdate(position, "OPENED");
        }
        catch (Exception ex)
        {
            OnError($"Robinhood execution failed for {orderId}: {ex.Message}", ex);
        }
    }

    private decimal CalculateRobinhoodFillPrice(LiveOrder order)
    {
        var basePrice = order.LimitPrice ?? 1.0m;
        
        // Robinhood often gets price improvement due to PFOF arrangements
        var priceImprovement = _random.NextDouble() < 0.3 ? 0.01m : 0m; // 30% chance
        var slippagePct = 0.015m; // Slightly better than IBKR due to PFOF
        
        if (order.Side == OrderSide.Buy || order.Side == OrderSide.BuyToClose)
            return basePrice * (1 + slippagePct) - priceImprovement;
        else
            return basePrice * (1 - slippagePct) + priceImprovement;
    }

    private LivePosition CreatePositionFromOrder(LiveOrder order)
    {
        var positionId = $"RH_POS_{Guid.NewGuid():N}";
        var legs = order.Legs.Select(leg => new PositionLeg
        {
            Symbol = CreateOptionSymbol(leg),
            Expiry = leg.Expiry,
            Strike = leg.Strike,
            Right = leg.Right,
            Quantity = leg.Side == OrderSide.Buy ? leg.Ratio : -leg.Ratio,
            AveragePrice = order.FilledPrice ?? 0m,
            CurrentPrice = order.FilledPrice ?? 0m
        }).ToList();
        
        return new LivePosition
        {
            PositionId = positionId,
            Underlying = order.Underlying,
            OpenedAt = DateTime.UtcNow,
            Legs = legs,
            NetCredit = order.FilledPrice ?? 0m,
            CurrentValue = order.FilledPrice ?? 0m,
            UnrealizedPnL = 0m
        };
    }

    private string CreateOptionSymbol(OrderLeg leg)
    {
        // Generate Robinhood-style option symbol
        var expiryStr = leg.Expiry.ToString("yyMMdd");
        var rightStr = leg.Right == Right.Call ? "C" : "P";
        var strikeStr = $"{leg.Strike:00000000}";
        return $"{leg.Symbol}{expiryStr}{rightStr}{strikeStr}";
    }

    private void EnsureConnected()
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to Robinhood or token expired");
    }

    // Simplified Black-Scholes for mock pricing (same as IBKR)
    private double CalculateOptionPrice(double spot, double strike, double timeToExpiry, double volatility, double riskFreeRate, Right right)
    {
        if (timeToExpiry <= 0)
            return Math.Max(0, right == Right.Call ? spot - strike : strike - spot);
        
        var d1 = (Math.Log(spot / strike) + (riskFreeRate + 0.5 * volatility * volatility) * timeToExpiry) / (volatility * Math.Sqrt(timeToExpiry));
        var d2 = d1 - volatility * Math.Sqrt(timeToExpiry);
        
        if (right == Right.Call)
            return spot * NormalCDF(d1) - strike * Math.Exp(-riskFreeRate * timeToExpiry) * NormalCDF(d2);
        else
            return strike * Math.Exp(-riskFreeRate * timeToExpiry) * NormalCDF(-d2) - spot * NormalCDF(-d1);
    }

    private double CalculateOptionDelta(double spot, double strike, double timeToExpiry, double volatility, double riskFreeRate, Right right)
    {
        if (timeToExpiry <= 0)
            return right == Right.Call ? (spot > strike ? 1 : 0) : (spot < strike ? -1 : 0);
        
        var d1 = (Math.Log(spot / strike) + (riskFreeRate + 0.5 * volatility * volatility) * timeToExpiry) / (volatility * Math.Sqrt(timeToExpiry));
        return right == Right.Call ? NormalCDF(d1) : NormalCDF(d1) - 1;
    }

    private static double NormalCDF(double x)
    {
        return 0.5 * (1.0 + Math.Sign(x) * Math.Sqrt(1.0 - Math.Exp(-2.0 * x * x / Math.PI)));
    }

    // Event helpers
    private void OnPositionUpdate(LivePosition position, string updateType)
    {
        PositionUpdated?.Invoke(this, new PositionUpdateEventArgs(position, updateType));
    }

    private void OnOrderUpdate(LiveOrder order, OrderStatus previousStatus)
    {
        OrderUpdated?.Invoke(this, new OrderUpdateEventArgs(order, previousStatus));
    }

    private void OnMarketDataUpdate(string symbol, decimal price, DateTime timestamp)
    {
        MarketDataUpdated?.Invoke(this, new MarketDataEventArgs(symbol, price, timestamp));
    }

    private void OnError(string message, Exception? exception = null, string severity = "ERROR")
    {
        ErrorOccurred?.Invoke(this, new ODTE.LiveTrading.Interfaces.ErrorEventArgs(message, exception, severity));
    }

    public void Dispose()
    {
        _marketDataTimer?.Dispose();
        _tokenRefreshTimer?.Dispose();
        _httpClient?.Dispose();
    }
}