using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WeatherMcp;

var builder = Host.CreateApplicationBuilder(args);

// MCP over stdio talks JSON-RPC on stdout, so all logs MUST go to stderr
// or they'll corrupt the protocol stream.
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

// Shared HttpClient for all Open-Meteo calls (free API, no key required).
builder.Services.AddSingleton(_ =>
{
    var client = new HttpClient();
    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("weather-mcp", "1.0"));
    return client;
});

builder.Services.AddSingleton<OpenMeteoClient>();

await builder.Build().RunAsync();
