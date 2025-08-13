using ODTE.Backtest.Core;

namespace ODTE.Backtest.Brokers.IBKR;

/// <summary>
/// Interface for Interactive Brokers integration
/// Provides methods to connect to TWS/Gateway and execute credit spread orders
/// </summary>
public interface IBroker
{
    /// <summary>
    /// Connect to Interactive Brokers TWS or Gateway
    /// </summary>
    /// <param name="host">TWS host (typically 127.0.0.1)</param>
    /// <param name="port">TWS port (7497 for paper, 7496 for live)</param>
    /// <param name="clientId">Unique client identifier</param>
    void Connect(string host, int port, int clientId);

    /// <summary>
    /// Disconnect from Interactive Brokers
    /// </summary>
    void Disconnect();

    /// <summary>
    /// Place a credit spread order (sell higher strike, buy lower strike for same expiry)
    /// </summary>
    /// <param name="order">The spread order containing contract details</param>
    /// <param name="quantity">Number of spreads to trade</param>
    /// <returns>Order ID assigned by IB</returns>
    int PlaceCreditSpread(SpreadOrder order, int quantity);

    /// <summary>
    /// Cancel an existing order
    /// </summary>
    /// <param name="orderId">IB order ID to cancel</param>
    void Cancel(int orderId);

    /// <summary>
    /// Gets whether the broker is currently connected
    /// </summary>
    bool IsConnected { get; }
}