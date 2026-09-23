# weather-mcp (.NET)

A Streamable HTTP MCP server, written in C#/.NET 8, exposing weather tools
backed by the free [Open-Meteo](https://open-meteo.com/) API. No API key
required.

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
├── Program.cs           # host setup, HTTP transport wiring
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
dotnet run --urls http://localhost:3001
```

The MCP Streamable HTTP endpoint is available at
`http://localhost:3001/mcp`. The server also exposes a simple status check at
`http://localhost:3001/status`.

## Test with MCP Inspector

```bash
npx @modelcontextprotocol/inspector
```

Select Streamable HTTP as the transport and enter
`http://localhost:3001/mcp` as the MCP server URL in the Inspector.

## Use with a Streamable HTTP MCP client

Start the server, then configure an HTTP MCP client with this URL:

```bash
http://localhost:3001/mcp
```

## Notes

- No API key needed — Open-Meteo's free tier is used for geocoding,
  forecast, and air-quality endpoints.
- The server listens on the default ASP.NET Core URL unless overridden with
  `--urls` or the `ASPNETCORE_URLS` environment variable.
