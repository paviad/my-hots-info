using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyHotsObserver;

var builder = Host.CreateApplicationBuilder(args);

// stdout carries the MCP protocol, so every log line goes to stderr.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton<GameWindow>();
builder.Services.AddSingleton<ObserverCapture>();
builder.Services.AddSingleton<ReplayClock>();
builder.Services.AddSingleton<ReplayCatalog>();

builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
