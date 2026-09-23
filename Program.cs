using System.Net.Http.Headers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WeatherMcp;


var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Information;
});

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

// Shared HttpClient for all Open-Meteo calls (free API, no key required).
builder.Services.AddSingleton(_ =>
{
    var client = new HttpClient();
    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("weather-mcp", "1.0"));
    return client;
});

builder.Services.AddSingleton<OpenMeteoClient>();

var app = builder.Build();
app.MapMcp("/mcp");
app.MapGet("/status", () => "Hello from WeatherMCP! Visit /mcp for the MCP server.");

await app.RunAsync();
