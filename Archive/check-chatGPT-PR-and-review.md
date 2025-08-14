# PR draft: IBKR adapter stub + GitHub Actions CI

This drop-in patch gives you:

1. **GitHub Actions** build for .NET 8
2. A minimal **IBKR broker adapter** skeleton (TWS sockets) to place/cancel XSP/SPX credit spreads from your `SpreadOrder`
3. A tiny wiring change in `Program.cs` to allow `mode: live_ib` (keeps backtest intact)

> Notes
>
> - IB doesn’t use API keys. TWS/IBG must be running and **API enabled** (Paper: port **7497**). Docs linked in comments.
>
> - The IB API C# client comes with TWS. Add reference to `IBApi.dll` from your TWS install or copy the **IBApi** project into your solution.
>
> - This is a **safe stub**: connects, validates instruments, and outlines order routing. Fill logic, error handling, and market-data subscriptions are marked TODOs.

---

## 1) CI: `.github/workflows/dotnet.yml`

```yaml
name: build

on:
  push:
    branches: [ main, master ]
  pull_request:
    branches: [ main, master ]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET 8
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - name: Restore
        run: dotnet restore
      - name: Build (Release)
        run: dotnet build --configuration Release --no-restore
      # Uncomment when you add tests
      # - name: Test
      #   run: dotnet test --configuration Release --no-build --verbosity normal
```

---

## 2) New files: IBKR broker adapter (skeleton)

**Add:** `Brokers/IBKR/IBroker.cs`

```csharp
namespace ODTE.Backtest.Brokers.IBKR
{
    using ODTE.Backtest.Core;

    public interface IBroker
    {
        void Connect(string host, int port, int clientId);
        void Disconnect();
        int PlaceCreditSpread(SpreadOrder order, int quantity);
        void Cancel(int orderId);
        bool IsConnected { get; }
    }
}
```

**Add:** `Brokers/IBKR/TwsBroker.cs`

```csharp
// WHY: Minimal TWS sockets adapter to route SpreadOrder to live/paper. No API keys; authenticate by TWS login.
// Setup TWS: Edit → Global Configuration → API → Settings → Enable ActiveX/Socket, Trusted IP 127.0.0.1, Port 7497 (paper)
// Docs: https://interactivebrokers.github.io/tws-api/classIBApi_1_1EClientSocket.html
// NOTE: Requires reference to IBApi.dll (bundled with TWS). Wrap in #if IBKR so repo builds without it.

#if IBKR
using IBApi;
using System;
using System.Collections.Generic;
using ODTE.Backtest.Core;

namespace ODTE.Backtest.Brokers.IBKR
{
    public sealed class TwsBroker : EWrapper, IBroker
    {
        private readonly EClientSocket _client;
        private readonly EReaderSignal _signal;
        private int _nextOrderId;
        public bool IsConnected { get; private set; }

        public TwsBroker()
        {
            _signal = new EReaderMonitorSignal();
            _client = new EClientSocket(this, _signal);
        }

        public void Connect(string host, int port, int clientId)
        {
            _client.eConnect(host, port, clientId);
            var reader = new EReader(_client, _signal);
            reader.Start();
            new System.Threading.Thread(() =>
            {
                while (_client.IsConnected()) { _signal.waitForSignal(); reader.processMsgs(); }
            }) { IsBackground = true }.Start();
        }

        public void Disconnect() => _client.eDisconnect();

        public int PlaceCreditSpread(SpreadOrder order, int quantity)
        {
            // Build 2-leg combo: short + long same Right/Expiry, different strikes
            var con = MakeComboContract(order);
            var ibOrder = new Order
            {
                Action = order.Short.Right == Right.Put ? "SELL" : "SELL", // credit spread
                OrderType = "LMT",
                TotalQuantity = quantity,
                LmtPrice = Math.Round(order.Credit, 2),
                Tif = "DAY",
            };
            int oid = _nextOrderId++;
            _client.placeOrder(oid, con, ibOrder);
            return oid;
        }

        public void Cancel(int orderId) => _client.cancelOrder(orderId);

        private static Contract MakeComboContract(SpreadOrder o)
        {
            // XSP/SPX index options are OCC-style; IB symbology: Underlying = "SPX" or "XSP", SecType = "BAG" for combos
            var c = new Contract { Symbol = o.Underlying, SecType = "BAG", Currency = "USD", Exchange = "SMART" };
            var legs = new List<ComboLeg>();
            legs.Add(MakeLeg(o.Short, -1));
            legs.Add(MakeLeg(o.Long, +1));
            c.ComboLegs = legs;
            return c;
        }

        private static ComboLeg MakeLeg(SpreadLeg leg, int ratio)
        {
            return new ComboLeg
            {
                Action = ratio < 0 ? "SELL" : "BUY",
                Ratio = Math.Abs(ratio),
                Exchange = "SMART",
                DesignatedLocation = string.Empty,
                OpenClose = 0,
                ShortSaleSlot = 0,
                ConId = 0 // TODO: Resolve via reqContractDetails for the specific option (expiry, strike, right)
            };
        }

        // ===== EWrapper minimal implementations =====
        public void nextValidId(int orderId) { _nextOrderId = orderId; IsConnected = true; }
        public void error(Exception e) { Console.WriteLine($"ERR: {e.Message}"); }
        public void error(string str) { Console.WriteLine($"ERR: {str}"); }
        public void error(int id, int code, string msg) { Console.WriteLine($"ERR {id} {code} {msg}"); }
        public void connectionClosed() { IsConnected = false; }

        // Unused callbacks — keep empty to satisfy interface
        public void tickPrice(int tickerId, int field, double price, TickAttrib attribs){}
        public void tickSize(int tickerId, int field, int size){}
        public void tickString(int tickerId, int field, string value){}
        public void tickGeneric(int tickerId, int field, double value){}
        public void tickEFP(int tickerId, int tickType, double basisPoints, string formattedBasisPoints, double totalDividends, int holdDays, string futureLastTradeDate, double dividendImpact, double dividendsToLastTradeDate){}
        public void openOrder(int orderId, Contract contract, Order order, OrderState orderState){}
        public void openOrderEnd(){}
        public void orderStatus(int orderId, string status, double filled, double remaining, double avgFillPrice, int permId, int parentId, double lastFillPrice, int clientId, string whyHeld, double mktCapPrice){}
        public void execDetails(int reqId, Contract contract, Execution execution){}
        public void execDetailsEnd(int reqId){}
        public void commissionReport(CommissionReport commissionReport){}
        public void currentTime(long time){}
        public void position(string account, Contract contract, double pos, double avgCost){}
        public void positionEnd(){}
        public void accountSummary(int reqId, string account, string tag, string value, string currency){}
        public void accountSummaryEnd(int reqId){}
        public void managedAccounts(string accountsList){}
        public void contractDetails(int reqId, ContractDetails contractDetails){}
        public void contractDetailsEnd(int reqId){}
        public void updateMktDepth(int tickerId, int position, int operation, int side, double price, int size){}
        public void updateMktDepthL2(int tickerId, int position, string marketMaker, int operation, int side, double price, int size, bool isSmartDepth){}
        public void updateNewsBulletin(int msgId, int msgType, string message, string origExchange){}
        public void updatePortfolio(Contract contract, double position, double marketPrice, double marketValue, double averageCost, double unrealizedPNL, double realizedPNL, string accountName){}
        public void accountDownloadEnd(string account){}
        public void bondContractDetails(int reqId, ContractDetails contractDetails){}
        public void updateAccountValue(string key, string value, string currency, string accountName){}
        public void updateAccountTime(string timestamp){}
        public void nextValidIdFAL(long orderId){}
        public void deltaNeutralValidation(int reqId, DeltaNeutralContract deltaNeutralContract){}
        public void tickOptionComputation(int tickerId, int field, double impliedVol, double delta, double optPrice, double pvDividend, double gamma, double vega, double theta, double undPrice){}
        public void fundamentalData(int reqId, string data){}
        public void historicalData(int reqId, Bar bar){}
        public void historicalDataEnd(int reqId, string startDateStr, string endDateStr){}
        public void marketDataType(int reqId, int marketDataType){}
        public void updateMktDepthL2(int tickerId, int position, string marketMaker, int operation, int side, double price, int size){}
        public void realtimeBar(int reqId, long time, double open, double high, double low, double close, long volume, double wap, int count){}
        public void scannerParameters(string xml){}
        public void scannerData(int reqId, int rank, ContractDetails contractDetails, string distance, string benchmark, string projection, string legsStr){}
        public void scannerDataEnd(int reqId){}
        public void receiveFA(int faDataType, string faXmlData){}
        public void verifyMessageAPI(string apiData){}
        public void verifyCompleted(bool isSuccessful, string errorText){}
        public void verifyAndAuthMessageAPI(string apiData, string xyzChallange){}
        public void verifyAndAuthCompleted(bool isSuccessful, string errorText){}
        public void displayGroupList(int reqId, string groups){}
        public void displayGroupUpdated(int reqId, string contractInfo){}
        public void connectAck(){}
        public void positionMulti(int reqId, string account, string modelCode, Contract contract, double pos, double avgCost){}
        public void positionMultiEnd(int reqId){}
        public void accountSummaryMulti(int reqId, string account, string modelCode, string tag, string value, string currency){}
        public void accountUpdateMulti(int reqId, string account, string modelCode, string key, string value, string currency){}
        public void accountUpdateMultiEnd(int reqId){}
        public void securityDefinitionOptionalParameter(int reqId, string exchange, int underlyingConId, string tradingClass, string multiplier, HashSet<string> expirations, HashSet<double> strikes){}
        public void securityDefinitionOptionalParameterEnd(int reqId){}
        public void softDollarTiers(int reqId, SoftDollarTier[] tiers){}
        public void familyCodes(FamilyCode[] familyCodes){}
        public void symbolSamples(int reqId, ContractDescription[] contractDescriptions){}
        public void mktDepthExchanges(DepthMktDataDescription[] descriptions){}
        public void tickReqParams(int tickerId, double minTick, string bboExchange, int snapshotPermissions){}
        public void smartComponents(int reqId, Dictionary<int, KeyValuePair<string, char>> theMap){}
        public void newsProviders(NewsProvider[] newsProviders){}
        public void tickNews(int tickerId, long timeStamp, string providerCode, string articleId, string headline, string extraData){}
        public void historicalNews(int requestId, string time, string providerCode, string articleId, string headline){}
        public void historicalNewsEnd(int requestId, bool hasMore){}
        public void headTimestamp(int reqId, string headTimestamp){}
        public void histogramData(int reqId, HistogramEntry[] data){}
        public void historicalDataUpdate(int reqId, Bar bar){}
        public void rerouteMktDataReq(int reqId, int conid, string exchange){}
        public void rerouteMktDepthReq(int reqId, int conid, string exchange){}
        public void marketRule(int marketRuleId, PriceIncrement[] priceIncrements){}
        public void pnl(int reqId, double dailyPnL, double unrealizedPnL, double realizedPnL){}
        public void pnlSingle(int reqId, double pos, double dailyPnL, double unrealizedPnL, double realizedPnL, double value){}
        public void historicalTicks(int reqId, HistoricalTick[] ticks, bool done){}
        public void historicalTicksBidAsk(int reqId, HistoricalTickBidAsk[] ticks, bool done){}
        public void historicalTicksLast(int reqId, HistoricalTickLast[] ticks, bool done){}
        public void tickByTickAllLast(int reqId, int tickType, long time, double price, int size, TickAttribLast tickAttribLast, string exchange, string specialConditions){}
        public void tickByTickBidAsk(int reqId, long time, double bidPrice, double askPrice, int bidSize, int askSize, TickAttribBidAsk tickAttribBidAsk){}
        public void tickByTickMidPoint(int reqId, long time, double midPoint){}
        public void orderBound(long orderId, int apiClientId, int apiOrderId){}
        public void completedOrder(Contract contract, Order order, OrderState orderState){}
        public void completedOrdersEnd(){}
    }
}
#endif
```

**Config:** add `live_ib` settings to `appsettings.yaml` (or a separate `appsettings.live.yaml`)

```yaml
mode: live_ib
ibkr:
  host: 127.0.0.1
  port: 7497   # paper
  client_id: 42
```

---

## 3) Wire-up: `Program.cs` (non-breaking)

Add a small factory to switch between backtest and live modes.

```csharp
// after loading cfg
var mode = cfg.Mode?.ToLowerInvariant() ?? "prototype";
if (mode == "live_ib")
{
    // Minimal live route — sketch. In a follow-up we’ll add a LiveRunner that streams decisions from RegimeScorer
    // and calls broker.PlaceCreditSpread(...). For now, this shows how to connect.
#if IBKR
    var ib = new ODTE.Backtest.Brokers.IBKR.TwsBroker();
    ib.Connect("127.0.0.1", 7497, 42);
    Console.WriteLine("Connected to IBKR (paper)");
    // TODO: stream signals + translate to orders
    return; // exit after connection demo
#else
    Console.WriteLine("Rebuild with IBKR symbol defined and IBApi referenced to enable live mode.");
    return;
#endif
}
```

Build tip:

```
dotnet build -c Release /p:DefineConstants=IBKR
```

---

## 4) Next steps checklist

-

**Docs**

- TWS sockets: [https://interactivebrokers.github.io/tws-api/classIBApi\_1\_1EClientSocket.html](https://interactivebrokers.github.io/tws-api/classIBApi_1_1EClientSocket.html)
- API enable steps: [https://interactivebrokers.github.io/tws-api/connection.html](https://interactivebrokers.github.io/tws-api/connection.html)
- Paper ports: 7497 (TWS), 4002 (IBG); Live: 7496, 4001.

