# weather-mcp (.NET)

A console-based (stdio) MCP server, written in C#/.NET 8, exposing weather
tools backed by the free [Open-Meteo](https://open-meteo.com/) API. No API
key required.

Uses the official [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)
(`ModelContextProtocol` on NuGet).

## Tools

| Tool | Description |
|---|---|
| `weather_geocode_location` | Look up coordinates/region for a place name |
| `weather_get_current` | Current temperature, humidity, wind, condition |
| `weather_get_forecast` | 1–16 day daily forecast (highs/lows, precip, wind) |
| `weather_get_air_quality` | US/European AQI and pollutant levels |

## Project layout

```
weather-mcp-dotnet/
├── WeatherMcp.csproj
├── Program.cs           # host setup, stdio transport wiring
├── WeatherTools.cs       # the four MCP tools
├── OpenMeteoClient.cs    # thin HTTP wrapper around Open-Meteo
└── WeatherCodes.cs       # WMO weather-code -> description lookup
```

## Build & run

Requires the .NET 8 SDK (you just installed this).

```bash
cd weather-mcp-dotnet
dotnet restore
dotnet build
dotnet run
```

Running it directly will sit waiting on stdin/stdout for MCP JSON-RPC
messages — that's expected. It's meant to be launched by an MCP client, not
used interactively on its own.

## Test with MCP Inspector

```bash
npx @modelcontextprotocol/inspector dotnet run --project weather-mcp-dotnet
```

This opens a browser UI where you can call each tool manually and inspect
the JSON responses.

## Use with Claude Desktop

First publish a standalone build so Claude Desktop doesn't need `dotnet run`
to resolve the project each time:

```bash
dotnet publish -c Release -o ./publish
```

Then add to `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "weather": {
      "command": "/absolute/path/to/weather-mcp-dotnet/publish/weather-mcp"
    }
  }
}
```

(On Windows the binary will be `weather-mcp.exe`.)

Restart Claude Desktop and the weather tools will show up in the tool list.

## Notes

- All logging is routed to **stderr** (`LogToStandardErrorThreshold`), since
  stdio MCP servers use stdout exclusively for the JSON-RPC protocol stream
  — writing a stray `Console.WriteLine` to stdout will corrupt it.
- No API key needed — Open-Meteo's free tier is used for geocoding,
  forecast, and air-quality endpoints.
- Targets `ModelContextProtocol` 1.3.0 (the current stable release as of
  writing). If a newer version is out when you build this, `dotnet add
  package ModelContextProtocol` will pull the latest automatically.
