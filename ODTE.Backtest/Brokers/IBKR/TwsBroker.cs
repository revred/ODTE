// WHY: Minimal TWS sockets adapter to route SpreadOrder to live/paper. No API keys; authenticate by TWS login.
// Setup TWS: Edit → Global Configuration → API → Settings → Enable ActiveX/Socket, Trusted IP 127.0.0.1, Port 7497 (paper)
// Docs: https://interactivebrokers.github.io/tws-api/classIBApi_1_1EClientSocket.html
// NOTE: Requires reference to IBApi.dll (bundled with TWS). Wrap in #if IBKR so repo builds without it.

#if IBKR
using IBApi;
using System;
using System.Collections.Generic;
using ODTE.Backtest.Core;

namespace ODTE.Backtest.Brokers.IBKR;

/// <summary>
/// Interactive Brokers TWS adapter for live trading
/// Implements the TWS socket API to place credit spread orders
/// 
/// REQUIREMENTS:
/// - TWS or IB Gateway must be running with API enabled
/// - IBApi.dll reference must be added to project
/// - Build with IBKR symbol defined: dotnet build -c Release /p:DefineConstants=IBKR
/// 
/// SETUP TWS:
/// 1. File → Global Configuration → API → Settings
/// 2. Enable ActiveX and Socket Clients
/// 3. Add 127.0.0.1 to Trusted IPs
/// 4. Set Socket port: 7497 (paper) or 7496 (live)
/// </summary>
public sealed class TwsBroker : EWrapper, IBroker
{
    private readonly EClientSocket _client;
    private readonly EReaderSignal _signal;
    private int _nextOrderId;
    private readonly object _lockObject = new object();

    public bool IsConnected { get; private set; }

    public TwsBroker()
    {
        _signal = new EReaderMonitorSignal();
        _client = new EClientSocket(this, _signal);
    }

    public void Connect(string host, int port, int clientId)
    {
        Console.WriteLine($"Connecting to IBKR TWS at {host}:{port} (client {clientId})...");
        
        _client.eConnect(host, port, clientId);
        
        if (!_client.IsConnected())
        {
            throw new InvalidOperationException("Failed to connect to TWS. Ensure TWS is running with API enabled.");
        }

        var reader = new EReader(_client, _signal);
        reader.Start();
        
        // Start message processing thread
        new System.Threading.Thread(() =>
        {
            while (_client.IsConnected()) 
            { 
                _signal.waitForSignal(); 
                reader.processMsgs(); 
            }
        }) { IsBackground = true }.Start();

        Console.WriteLine("Connected to IBKR TWS successfully.");
    }

    public void Disconnect()
    {
        Console.WriteLine("Disconnecting from IBKR TWS...");
        _client.eDisconnect();
        IsConnected = false;
    }

    public int PlaceCreditSpread(SpreadOrder order, int quantity)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to TWS");

        Console.WriteLine($"Placing credit spread: {order.Underlying} {order.Short.Strike}/{order.Long.Strike} {order.Short.Right} @ ${order.Credit:F2}");

        // Build 2-leg combo contract: short + long same Right/Expiry, different strikes
        var contract = MakeComboContract(order);
        
        var ibOrder = new Order
        {
            Action = "SELL", // Credit spread = sell the combo
            OrderType = "LMT",
            TotalQuantity = quantity,
            LmtPrice = Math.Round(order.Credit, 2),
            Tif = "DAY",
        };

        int orderId;
        lock (_lockObject)
        {
            orderId = _nextOrderId++;
        }

        _client.placeOrder(orderId, contract, ibOrder);
        Console.WriteLine($"Order placed with ID: {orderId}");
        
        return orderId;
    }

    public void Cancel(int orderId)
    {
        Console.WriteLine($"Cancelling order {orderId}");
        _client.cancelOrder(orderId);
    }

    private static Contract MakeComboContract(SpreadOrder order)
    {
        // XSP/SPX index options are OCC-style; IB symbology: Underlying = "SPX" or "XSP", SecType = "BAG" for combos
        var contract = new Contract 
        { 
            Symbol = order.Underlying, 
            SecType = "BAG", 
            Currency = "USD", 
            Exchange = "SMART" 
        };

        var legs = new List<ComboLeg>
        {
            MakeLeg(order.Short, -1), // Sell short leg
            MakeLeg(order.Long, +1)   // Buy long leg
        };

        contract.ComboLegs = legs;
        return contract;
    }

    private static ComboLeg MakeLeg(SpreadLeg leg, int ratio)
    {
        return new ComboLeg
        {
            Action = ratio < 0 ? "SELL" : "BUY",
            Ratio = Math.Abs(ratio),
            Exchange = "SMART",
            DesignatedLocation = string.Empty,
            OpenClose = 0, // Open position
            ShortSaleSlot = 0,
            ConId = 0 // TODO: Resolve via reqContractDetails for the specific option (expiry, strike, right)
        };
    }

    // ===== EWrapper Required Implementations =====
    
    public void nextValidId(int orderId) 
    { 
        _nextOrderId = orderId; 
        IsConnected = true;
        Console.WriteLine($"Received next valid order ID: {orderId}");
    }

    public void error(Exception e) 
    { 
        Console.WriteLine($"IBKR Error (Exception): {e.Message}"); 
    }

    public void error(string str) 
    { 
        Console.WriteLine($"IBKR Error (String): {str}"); 
    }

    public void error(int id, int code, string msg) 
    { 
        Console.WriteLine($"IBKR Error {id} {code}: {msg}");
        
        // Handle connection errors
        if (code == 502) // Couldn't connect to TWS
        {
            IsConnected = false;
        }
    }

    public void connectionClosed() 
    { 
        IsConnected = false;
        Console.WriteLine("Connection to TWS closed");
    }

    public void orderStatus(int orderId, string status, double filled, double remaining, double avgFillPrice, int permId, int parentId, double lastFillPrice, int clientId, string whyHeld, double mktCapPrice)
    {
        Console.WriteLine($"Order {orderId} status: {status} (filled: {filled}, remaining: {remaining}, avg price: {avgFillPrice:F2})");
    }

    public void openOrder(int orderId, Contract contract, Order order, OrderState orderState)
    {
        Console.WriteLine($"Open order {orderId}: {contract.Symbol} {order.Action} {order.TotalQuantity} @ {order.LmtPrice}");
    }

    public void execDetails(int reqId, Contract contract, Execution execution)
    {
        Console.WriteLine($"Execution: {contract.Symbol} {execution.Side} {execution.Shares} @ {execution.Price} ({execution.Time})");
    }

    // ===== Unused EWrapper Methods (Required by Interface) =====
    public void tickPrice(int tickerId, int field, double price, TickAttrib attribs) { }
    public void tickSize(int tickerId, int field, int size) { }
    public void tickString(int tickerId, int field, string value) { }
    public void tickGeneric(int tickerId, int field, double value) { }
    public void tickEFP(int tickerId, int tickType, double basisPoints, string formattedBasisPoints, double totalDividends, int holdDays, string futureLastTradeDate, double dividendImpact, double dividendsToLastTradeDate) { }
    public void openOrderEnd() { }
    public void execDetailsEnd(int reqId) { }
    public void commissionReport(CommissionReport commissionReport) { }
    public void currentTime(long time) { }
    public void position(string account, Contract contract, double pos, double avgCost) { }
    public void positionEnd() { }
    public void accountSummary(int reqId, string account, string tag, string value, string currency) { }
    public void accountSummaryEnd(int reqId) { }
    public void managedAccounts(string accountsList) { }
    public void contractDetails(int reqId, ContractDetails contractDetails) { }
    public void contractDetailsEnd(int reqId) { }
    public void updateMktDepth(int tickerId, int position, int operation, int side, double price, int size) { }
    public void updateMktDepthL2(int tickerId, int position, string marketMaker, int operation, int side, double price, int size, bool isSmartDepth) { }
    public void updateNewsBulletin(int msgId, int msgType, string message, string origExchange) { }
    public void updatePortfolio(Contract contract, double position, double marketPrice, double marketValue, double averageCost, double unrealizedPNL, double realizedPNL, string accountName) { }
    public void accountDownloadEnd(string account) { }
    public void bondContractDetails(int reqId, ContractDetails contractDetails) { }
    public void updateAccountValue(string key, string value, string currency, string accountName) { }
    public void updateAccountTime(string timestamp) { }
    public void nextValidIdFAL(long orderId) { }
    public void deltaNeutralValidation(int reqId, DeltaNeutralContract deltaNeutralContract) { }
    public void tickOptionComputation(int tickerId, int field, double impliedVol, double delta, double optPrice, double pvDividend, double gamma, double vega, double theta, double undPrice) { }
    public void fundamentalData(int reqId, string data) { }
    public void historicalData(int reqId, Bar bar) { }
    public void historicalDataEnd(int reqId, string startDateStr, string endDateStr) { }
    public void marketDataType(int reqId, int marketDataType) { }
    public void updateMktDepthL2(int tickerId, int position, string marketMaker, int operation, int side, double price, int size) { }
    public void realtimeBar(int reqId, long time, double open, double high, double low, double close, long volume, double wap, int count) { }
    public void scannerParameters(string xml) { }
    public void scannerData(int reqId, int rank, ContractDetails contractDetails, string distance, string benchmark, string projection, string legsStr) { }
    public void scannerDataEnd(int reqId) { }
    public void receiveFA(int faDataType, string faXmlData) { }
    public void verifyMessageAPI(string apiData) { }
    public void verifyCompleted(bool isSuccessful, string errorText) { }
    public void verifyAndAuthMessageAPI(string apiData, string xyzChallange) { }
    public void verifyAndAuthCompleted(bool isSuccessful, string errorText) { }
    public void displayGroupList(int reqId, string groups) { }
    public void displayGroupUpdated(int reqId, string contractInfo) { }
    public void connectAck() { }
    public void positionMulti(int reqId, string account, string modelCode, Contract contract, double pos, double avgCost) { }
    public void positionMultiEnd(int reqId) { }
    public void accountSummaryMulti(int reqId, string account, string modelCode, string tag, string value, string currency) { }
    public void accountUpdateMulti(int reqId, string account, string modelCode, string key, string value, string currency) { }
    public void accountUpdateMultiEnd(int reqId) { }
    public void securityDefinitionOptionalParameter(int reqId, string exchange, int underlyingConId, string tradingClass, string multiplier, HashSet<string> expirations, HashSet<double> strikes) { }
    public void securityDefinitionOptionalParameterEnd(int reqId) { }
    public void softDollarTiers(int reqId, SoftDollarTier[] tiers) { }
    public void familyCodes(FamilyCode[] familyCodes) { }
    public void symbolSamples(int reqId, ContractDescription[] contractDescriptions) { }
    public void mktDepthExchanges(DepthMktDataDescription[] descriptions) { }
    public void tickReqParams(int tickerId, double minTick, string bboExchange, int snapshotPermissions) { }
    public void smartComponents(int reqId, Dictionary<int, KeyValuePair<string, char>> theMap) { }
    public void newsProviders(NewsProvider[] newsProviders) { }
    public void tickNews(int tickerId, long timeStamp, string providerCode, string articleId, string headline, string extraData) { }
    public void historicalNews(int requestId, string time, string providerCode, string articleId, string headline) { }
    public void historicalNewsEnd(int requestId, bool hasMore) { }
    public void headTimestamp(int reqId, string headTimestamp) { }
    public void histogramData(int reqId, HistogramEntry[] data) { }
    public void historicalDataUpdate(int reqId, Bar bar) { }
    public void rerouteMktDataReq(int reqId, int conid, string exchange) { }
    public void rerouteMktDepthReq(int reqId, int conid, string exchange) { }
    public void marketRule(int marketRuleId, PriceIncrement[] priceIncrements) { }
    public void pnl(int reqId, double dailyPnL, double unrealizedPnL, double realizedPnL) { }
    public void pnlSingle(int reqId, double pos, double dailyPnL, double unrealizedPnL, double realizedPnL, double value) { }
    public void historicalTicks(int reqId, HistoricalTick[] ticks, bool done) { }
    public void historicalTicksBidAsk(int reqId, HistoricalTickBidAsk[] ticks, bool done) { }
    public void historicalTicksLast(int reqId, HistoricalTickLast[] ticks, bool done) { }
    public void tickByTickAllLast(int reqId, int tickType, long time, double price, int size, TickAttribLast tickAttribLast, string exchange, string specialConditions) { }
    public void tickByTickBidAsk(int reqId, long time, double bidPrice, double askPrice, int bidSize, int askSize, TickAttribBidAsk tickAttribBidAsk) { }
    public void tickByTickMidPoint(int reqId, long time, double midPoint) { }
    public void orderBound(long orderId, int apiClientId, int apiOrderId) { }
    public void completedOrder(Contract contract, Order order, OrderState orderState) { }
    public void completedOrdersEnd() { }
}
#else
using ODTE.Backtest.Core;

namespace ODTE.Backtest.Brokers.IBKR;

/// <summary>
/// Stub implementation when IBKR symbol is not defined
/// </summary>
public sealed class TwsBroker : IBroker
{
    public bool IsConnected => false;

    public void Connect(string host, int port, int clientId)
    {
        throw new NotSupportedException("IBKR integration not available. Build with IBKR symbol defined and IBApi referenced.");
    }

    public void Disconnect() { }

    public int PlaceCreditSpread(SpreadOrder order, int quantity)
    {
        throw new NotSupportedException("IBKR integration not available. Build with IBKR symbol defined and IBApi referenced.");
    }

    public void Cancel(int orderId) { }
}
#endif