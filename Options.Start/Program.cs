using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Options.Start;
using Options.Start.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Http client for API calls
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Local storage for client-side persistence
builder.Services.AddBlazoredLocalStorage();

// Application services
builder.Services.AddScoped<IOptimizationService, OptimizationService>();
builder.Services.AddScoped<IBacktestService, BacktestService>();
builder.Services.AddScoped<ITradingService, TradingService>();
builder.Services.AddScoped<IDataService, DataService>();
builder.Services.AddScoped<IRiskService, RiskService>();

// SignalR for real-time updates (when server component is added)
// builder.Services.AddScoped<INotificationService, SignalRNotificationService>();

await builder.Build().RunAsync();