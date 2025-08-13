using ODTE.Backtest.Core;
using ODTE.LiveTrading.Interfaces;
using System.Collections.Concurrent;

namespace ODTE.LiveTrading.Brokers;

/// <summary>
/// Mock implementation of Interactive Brokers UK API for testing and development.
/// 
/// FEATURES SIMULATED:
/// - TWS/Gateway connection simulation
/// - Real-time options chain generation
/// - Order execution with realistic slippage
/// - Position tracking and Greeks calculation
/// - IBKR-specific features (portfolio margin, complex orders)
/// 
/// IBKR-SPECIFIC BEHAVIORS:
/// - Options approval levels (0-5)
/// - Pattern Day Trader rules
/// - Portfolio margin calculations
/// - Complex order types (spreads, condors)
/// - Real-time risk monitoring
/// 
/// LIMITATIONS:
/// - This is a SIMULATION for testing only
/// - Does not connect to real IBKR systems
/// - Uses synthetic market data
/// - No real money involved
/// 
/// PRODUCTION INTEGRATION:
/// Replace this mock with real TWS API client:
/// - Install IBKR TWS or Gateway
/// - Use IBApi NuGet package
/// - Implement IB-specific authentication
/// - Handle connection management and heartbeats
/// 
/// References:
/// - IBKR TWS API: https://interactivebrokers.github.io/tws-api/
/// - Portfolio Margin: https://www.interactivebrokers.com/en/trading/margin-portfolio.php
/// - Options Trading: https://www.interactivebrokers.com/en/trading/options.php
/// </summary>
public class IBKRMockBroker : IBroker
{
    private readonly Random _random = new(42); // Deterministic for testing
    private readonly ConcurrentDictionary<string, LiveOrder> _orders = new();
    private readonly ConcurrentDictionary<string, LivePosition> _positions = new();
    private readonly Timer _marketDataTimer;
    private readonly Timer _heartbeatTimer;
    
    // IBKR-specific state
    private bool _isConnected = false;
    private BrokerCredentials? _credentials;
    private AccountInfo? _accountInfo;
    private DateTime _lastHeartbeat = DateTime.UtcNow;
    
    // Market simulation state
    private decimal _currentSpotPrice = 4900.0m; // SPX simulation
    private readonly Dictionary<string, decimal> _spotPrices = new();
    private int _nextOrderId = 1;

    public string BrokerName => "Interactive Brokers UK (Mock)";
    public bool IsConnected => _isConnected;
    public DateTime LastHeartbeat => _lastHeartbeat;

    // Events
    public event EventHandler<PositionUpdateEventArgs>? PositionUpdated;
    public event EventHandler<OrderUpdateEventArgs>? OrderUpdated;
    public event EventHandler<MarketDataEventArgs>? MarketDataUpdated;
    public event EventHandler<ODTE.LiveTrading.Interfaces.ErrorEventArgs>? ErrorOccurred;

    public IBKRMockBroker()
    {
        // Initialize spot prices for common underlyings
        _spotPrices["SPX"] = 4900.0m;
        _spotPrices["XSP"] = 490.0m; // 1/10th of SPX
        _spotPrices["SPY"] = 490.0m;
        
        // Start market data simulation (updates every 5 seconds)
        _marketDataTimer = new Timer(SimulateMarketData, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        
        // Heartbeat every 30 seconds
        _heartbeatTimer = new Timer(SendHeartbeat, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public async Task<bool> ConnectAsync(BrokerCredentials credentials)
    {
        try
        {
            // Simulate IBKR connection process
            await SimulateConnectionDelay();
            
            // Validate credentials (mock validation)
            if (string.IsNullOrWhiteSpace(credentials.Username) || 
                string.IsNullOrWhiteSpace(credentials.ApiKey))
            {
                OnError("Invalid credentials provided");
                return false;
            }

            _credentials = credentials;
            _isConnected = true;
            _lastHeartbeat = DateTime.UtcNow;

            // Initialize mock account
            _accountInfo = CreateMockAccountInfo(credentials);
            
            OnError($"Connected to IBKR TWS (Mock) - Account: {_accountInfo.AccountId}", severity: "INFO");
            return true;
        }
        catch (Exception ex)
        {
            OnError($"Connection failed: {ex.Message}", ex);
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        _isConnected = false;
        _credentials = null;
        _accountInfo = null;
        
        // Clear positions and orders on disconnect
        _positions.Clear();
        _orders.Clear();
        
        OnError("Disconnected from IBKR TWS (Mock)", severity: "INFO");
        await Task.CompletedTask;
    }

    public async Task<AccountInfo> GetAccountInfoAsync()
    {
        EnsureConnected();
        
        // Simulate slight delay
        await Task.Delay(100);
        
        // Update account with current P&L
        var totalPnL = _positions.Values.Sum(p => p.UnrealizedPnL);
        var updatedAccount = _accountInfo! with
        {
            NetLiquidationValue = _accountInfo.NetLiquidationValue + totalPnL,
            AvailableFunds = _accountInfo.AvailableFunds + totalPnL * 0.8m // Conservative available funds
        };
        
        _accountInfo = updatedAccount;
        return _accountInfo;
    }

    public async Task<IEnumerable<OptionQuote>> GetOptionChainAsync(string underlying, DateTime expiry)
    {
        EnsureConnected();
        
        await Task.Delay(200); // Simulate API latency
        
        var spot = _spotPrices.GetValueOrDefault(underlying, 100m);
        var quotes = new List<OptionQuote>();
        var now = DateTime.UtcNow;
        var expiryDate = DateOnly.FromDateTime(expiry);
        
        // Generate realistic option chain
        var timeToExpiry = (expiry - now).TotalDays / 365.0;
        var volatility = GetImpliedVolatility(underlying);
        
        // Create strikes around current spot (typically ±20% range)
        var minStrike = spot * 0.8m;
        var maxStrike = spot * 1.2m;
        var strikeIncrement = underlying == "XSP" ? 0.5m : 5m; // XSP has smaller increments
        
        for (var strike = minStrike; strike <= maxStrike; strike += strikeIncrement)
        {
            var strikeDouble = (double)strike;
            var spotDouble = (double)spot;
            
            // Calculate theoretical values using Black-Scholes
            var callPrice = CalculateOptionPrice(spotDouble, strikeDouble, timeToExpiry, volatility, 0.05, Right.Call);
            var putPrice = CalculateOptionPrice(spotDouble, strikeDouble, timeToExpiry, volatility, 0.05, Right.Put);
            
            var callDelta = CalculateOptionDelta(spotDouble, strikeDouble, timeToExpiry, volatility, 0.05, Right.Call);
            var putDelta = CalculateOptionDelta(spotDouble, strikeDouble, timeToExpiry, volatility, 0.05, Right.Put);
            
            // Add bid-ask spread (realistic IBKR spreads)
            var callSpread = Math.Max(0.05, callPrice * 0.02); // Min $0.05 or 2% of mid
            var putSpread = Math.Max(0.05, putPrice * 0.02);
            
            // Call option
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
            
            // Put option
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
        await Task.Delay(50); // Simulate real-time data latency
        
        return (double)_spotPrices.GetValueOrDefault(underlying, 100m);
    }

    public async Task<MarketStatus> GetMarketStatusAsync()
    {
        await Task.Delay(25);
        
        var now = DateTime.UtcNow;
        var easternTime = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));
        var hour = easternTime.Hour;
        var dayOfWeek = easternTime.DayOfWeek;
        
        // Market hours: 9:30 AM - 4:00 PM ET, Monday-Friday
        var isMarketDay = dayOfWeek >= DayOfWeek.Monday && dayOfWeek <= DayOfWeek.Friday;
        var isMarketHours = hour >= 9 && hour < 16; // Simplified
        var isOpen = isMarketDay && isMarketHours;
        
        return new MarketStatus
        {
            IsOpen = isOpen,
            Session = isOpen ? "REGULAR" : "CLOSED",
            ProductStatus = new Dictionary<string, bool>
            {
                ["Stocks"] = isOpen,
                ["Options"] = isOpen,
                ["Futures"] = isOpen
            }
        };
    }

    public async Task<OrderResult> SubmitOrderAsync(LiveOrder order)
    {
        EnsureConnected();
        
        try
        {
            // Simulate order validation delay
            await Task.Delay(100);
            
            // Validate order
            var validation = await ValidateOrderAsync(order);
            if (!validation)
            {
                return new OrderResult
                {
                    Success = false,
                    Message = "Order validation failed",
                    Status = OrderStatus.Rejected
                };
            }
            
            // Assign order ID and update status
            var orderId = $"IBKR_{_nextOrderId++}";
            var submittedOrder = order with 
            { 
                OrderId = orderId, 
                Status = OrderStatus.Submitted 
            };
            
            _orders[orderId] = submittedOrder;
            
            // Simulate fill after short delay (mock execution)
            _ = Task.Run(async () =>
            {
                await Task.Delay(2000); // 2 second fill simulation
                await ExecuteOrder(orderId);
            });
            
            OnOrderUpdate(submittedOrder, OrderStatus.Pending);
            
            return new OrderResult
            {
                Success = true,
                OrderId = orderId,
                Message = "Order submitted successfully",
                Status = OrderStatus.Submitted
            };
        }
        catch (Exception ex)
        {
            OnError($"Order submission failed: {ex.Message}", ex);
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
        await Task.Delay(50);
        
        if (_orders.TryGetValue(orderId, out var order))
        {
            if (order.Status == OrderStatus.Submitted || order.Status == OrderStatus.PartiallyFilled)
            {
                var cancelledOrder = order with { Status = OrderStatus.Cancelled };
                _orders[orderId] = cancelledOrder;
                
                OnOrderUpdate(cancelledOrder, order.Status);
                
                return new OrderResult
                {
                    Success = true,
                    OrderId = orderId,
                    Message = "Order cancelled successfully",
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
        // Cancel existing and submit new (simplified approach)
        var cancelResult = await CancelOrderAsync(orderId);
        if (!cancelResult.Success)
            return cancelResult;
            
        return await SubmitOrderAsync(newOrder);
    }

    public async Task<IEnumerable<LivePosition>> GetPositionsAsync()
    {
        EnsureConnected();
        await Task.Delay(75);
        
        // Update positions with current market values
        foreach (var position in _positions.Values.ToList())
        {
            if (position.IsOpen)
            {
                var updatedPosition = await UpdatePositionValue(position);
                _positions[position.PositionId] = updatedPosition;
            }
        }
        
        return _positions.Values.Where(p => p.IsOpen);
    }

    public async Task<IEnumerable<LiveOrder>> GetOrdersAsync(OrderStatus? status = null)
    {
        EnsureConnected();
        await Task.Delay(50);
        
        var orders = _orders.Values.AsEnumerable();
        
        if (status.HasValue)
            orders = orders.Where(o => o.Status == status.Value);
            
        return orders.OrderByDescending(o => o.CreatedAt);
    }

    public async Task<LiveOrder?> GetOrderAsync(string orderId)
    {
        EnsureConnected();
        await Task.Delay(25);
        
        return _orders.GetValueOrDefault(orderId);
    }

    public async Task<RiskLimits> GetRiskLimitsAsync()
    {
        EnsureConnected();
        await Task.Delay(50);
        
        // IBKR-style risk limits
        return new RiskLimits
        {
            MaxOrderValue = _accountInfo?.BuyingPower ?? 10000m,
            DailyLossLimit = (_accountInfo?.NetLiquidationValue ?? 25000m) * 0.05m, // 5% daily loss limit
            MaxPositions = 50,
            MaxDelta = 100,
            MaxGamma = 50,
            AllowNakedOptions = _accountInfo?.MaxOptionsLevel >= 4, // Level 4+ for naked options
            ProductLimits = new Dictionary<string, decimal>
            {
                ["Options"] = 25000m,
                ["Spreads"] = 50000m,
                ["Futures"] = 10000m
            }
        };
    }

    public async Task<bool> ValidateOrderAsync(LiveOrder order)
    {
        EnsureConnected();
        
        // Basic validation checks
        if (order.Quantity <= 0)
            return false;
            
        if (string.IsNullOrWhiteSpace(order.Underlying))
            return false;
            
        // Check account permissions
        if (_accountInfo?.OptionsLevel == false && order.Legs.Any())
            return false;
            
        // Check risk limits
        var riskLimits = await GetRiskLimitsAsync();
        var orderValue = EstimateOrderValue(order);
        
        if (orderValue > riskLimits.MaxOrderValue)
            return false;
            
        return true;
    }

    // Helper methods
    private void EnsureConnected()
    {
        if (!_isConnected)
            throw new InvalidOperationException("Not connected to IBKR TWS");
    }

    private async Task SimulateConnectionDelay()
    {
        // Simulate realistic IBKR connection time
        await Task.Delay(_random.Next(1000, 3000));
    }

    private AccountInfo CreateMockAccountInfo(BrokerCredentials credentials)
    {
        var isPaper = credentials.PaperTrading;
        var baseValue = isPaper ? 100000m : 25000m; // Paper gets more virtual money
        
        return new AccountInfo
        {
            AccountId = isPaper ? $"DU{_random.Next(1000000, 9999999)}" : $"U{_random.Next(1000000, 9999999)}",
            NetLiquidationValue = baseValue,
            AvailableFunds = baseValue * 0.8m,
            BuyingPower = baseValue * (isPaper ? 4m : 2m), // More buying power for paper
            MaintenanceMargin = 0m,
            OptionsLevel = true,
            MaxOptionsLevel = isPaper ? 5 : 3, // Paper trading gets full permissions
            PatternDayTrader = baseValue >= 25000m,
            Balances = new Dictionary<string, decimal>
            {
                ["USD"] = baseValue,
                ["Equity"] = 0m,
                ["Options"] = 0m
            }
        };
    }

    private void SimulateMarketData(object? state)
    {
        if (!_isConnected) return;
        
        // Simulate realistic price movement
        foreach (var (symbol, currentPrice) in _spotPrices.ToList())
        {
            // Random walk with slight upward bias (0.1% per update)
            var change = (decimal)(_random.NextDouble() - 0.49) * currentPrice * 0.001m;
            var newPrice = Math.Max(1m, currentPrice + change);
            
            _spotPrices[symbol] = newPrice;
            
            OnMarketDataUpdate(symbol, newPrice, DateTime.UtcNow);
        }
    }

    private void SendHeartbeat(object? state)
    {
        if (_isConnected)
        {
            _lastHeartbeat = DateTime.UtcNow;
        }
    }

    private async Task ExecuteOrder(string orderId)
    {
        if (!_orders.TryGetValue(orderId, out var order))
            return;
            
        try
        {
            // Simulate realistic fill price with slippage
            var fillPrice = CalculateFillPrice(order);
            var filledOrder = order with 
            { 
                Status = OrderStatus.Filled, 
                FilledAt = DateTime.UtcNow,
                FilledPrice = fillPrice,
                FilledQuantity = order.Quantity
            };
            
            _orders[orderId] = filledOrder;
            
            // Create corresponding position
            var position = CreatePositionFromOrder(filledOrder);
            _positions[position.PositionId] = position;
            
            OnOrderUpdate(filledOrder, OrderStatus.Submitted);
            OnPositionUpdate(position, "OPENED");
        }
        catch (Exception ex)
        {
            OnError($"Order execution failed for {orderId}: {ex.Message}", ex);
        }
    }

    private decimal CalculateFillPrice(LiveOrder order)
    {
        // Simulate slippage based on order type and market conditions
        var basePrice = order.LimitPrice ?? 1.0m;
        var slippagePct = 0.02m; // 2% typical slippage for options spreads
        
        if (order.Side == OrderSide.Buy || order.Side == OrderSide.BuyToClose)
            return basePrice * (1 + slippagePct); // Pay more when buying
        else
            return basePrice * (1 - slippagePct); // Receive less when selling
    }

    private LivePosition CreatePositionFromOrder(LiveOrder order)
    {
        var positionId = $"POS_{Guid.NewGuid():N}";
        var legs = order.Legs.Select(leg => new PositionLeg
        {
            Symbol = leg.Symbol,
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
            UnrealizedPnL = 0m,
            RealizedPnL = 0m
        };
    }

    private async Task<LivePosition> UpdatePositionValue(LivePosition position)
    {
        // Simplified P&L calculation
        var currentValue = position.NetCredit; // Would calculate from current option prices
        var unrealizedPnL = currentValue - position.NetCredit;
        
        return position with 
        { 
            CurrentValue = currentValue,
            UnrealizedPnL = unrealizedPnL
        };
    }

    private decimal EstimateOrderValue(LiveOrder order)
    {
        // Rough estimate for validation
        return (order.LimitPrice ?? 1.0m) * order.Quantity * 100m; // Options are per 100 shares
    }

    private double GetImpliedVolatility(string underlying)
    {
        // Simulate realistic IV levels
        return underlying switch
        {
            "SPX" or "XSP" or "SPY" => 0.15 + _random.NextDouble() * 0.10, // 15-25%
            "VIX" => 0.60 + _random.NextDouble() * 0.40, // 60-100%
            _ => 0.20 + _random.NextDouble() * 0.15 // 20-35%
        };
    }

    // Simplified Black-Scholes implementation for mock pricing
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
        _heartbeatTimer?.Dispose();
    }
}