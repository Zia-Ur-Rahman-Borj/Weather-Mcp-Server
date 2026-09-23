using System.ComponentModel;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;

namespace WeatherMcp;

public enum ResponseFormat
{
    Markdown,
    Json
}

[McpServerToolType]
public static class WeatherTools
{
    [McpServerTool(
        Name = "weather_geocode_location",
        Title = "Search for a Location",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true)]
    [Description(
        "Search for a place name and return matching locations with coordinates. Use this " +
        "first when a location name is ambiguous, or when you need the latitude/longitude, " +
        "country, or timezone for a place before calling other weather tools.")]
    public static async Task<string> GeocodeLocation(
        OpenMeteoClient client,
        [Description("Free-text place name to search for, e.g. 'Lahore', 'Paris, France', 'Dubai'.")]
        string location,
        [Description("Maximum number of matching locations to return (1-20).")]
        int count = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return Error("location must not be empty.");
        }
        count = Math.Clamp(count, 1, 20);

        try
        {
            var matches = await client.GeocodeAsync(location, count, cancellationToken);
            if (matches.Count == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    error = $"No locations found matching '{location}'.",
                    results = Array.Empty<object>()
                });
            }

            var results = matches.Select(m => new
            {
                name = m.Name,
                country = m.Country,
                admin1 = m.Admin1,
                latitude = m.Latitude,
                longitude = m.Longitude,
                timezone = m.Timezone,
                population = m.Population
            });

            return JsonSerializer.Serialize(new { query = location, results }, JsonOptions);
        }
        catch (Exception ex)
        {
            return Error(DescribeException(ex));
        }
    }

    [McpServerTool(
        Name = "weather_get_current",
        Title = "Get Current Weather",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true)]
    [Description(
        "Get the current weather conditions for a named location: temperature, feels-like " +
        "temperature, humidity, wind, precipitation, and a plain-language sky condition.")]
    public static async Task<string> GetCurrentWeather(
        OpenMeteoClient client,
        [Description("Free-text place name, e.g. 'Lahore', 'New York', 'Riyadh, Saudi Arabia'.")]
        string location,
        [Description("Temperature unit: 'celsius' or 'fahrenheit'.")]
        string temperatureUnit = "celsius",
        [Description("Output format: 'markdown' for human-readable or 'json' for machine-readable.")]
        ResponseFormat responseFormat = ResponseFormat.Markdown,
        CancellationToken cancellationToken = default)
    {
        var unitError = ValidateUnit(temperatureUnit);
        if (unitError is not null) return Error(unitError);

        try
        {
            var loc = await ResolveLocationAsync(client, location, cancellationToken);
            if (loc is null)
            {
                return NotFound(location);
            }

            var data = await client.GetCurrentWeatherAsync(loc.Latitude, loc.Longitude, temperatureUnit, cancellationToken);
            var current = data?["current"];
            if (current is null)
            {
                return Error("Weather service returned no current-conditions data.");
            }

            var unitSymbol = temperatureUnit == "fahrenheit" ? "\u00b0F" : "\u00b0C";
            var condition = WeatherCodes.Describe(current["weather_code"]?.GetValue<int>());

            if (responseFormat == ResponseFormat.Json)
            {
                return JsonSerializer.Serialize(new
                {
                    location = loc.DisplayName,
                    latitude = loc.Latitude,
                    longitude = loc.Longitude,
                    timezone = data?["timezone"]?.GetValue<string>(),
                    observed_at = current["time"]?.GetValue<string>(),
                    condition,
                    temperature = current["temperature_2m"]?.GetValue<double>(),
                    apparent_temperature = current["apparent_temperature"]?.GetValue<double>(),
                    temperature_unit = unitSymbol,
                    humidity_percent = current["relative_humidity_2m"]?.GetValue<double>(),
                    precipitation_mm = current["precipitation"]?.GetValue<double>(),
                    wind_speed_kmh = current["wind_speed_10m"]?.GetValue<double>(),
                    wind_direction_deg = current["wind_direction_10m"]?.GetValue<double>()
                }, JsonOptions);
            }

            var sb = new StringBuilder();
            sb.AppendLine($"## Current weather in {loc.DisplayName}");
            sb.AppendLine($"- **Condition:** {condition}");
            sb.AppendLine($"- **Temperature:** {current["temperature_2m"]}{unitSymbol} (feels like {current["apparent_temperature"]}{unitSymbol})");
            sb.AppendLine($"- **Humidity:** {current["relative_humidity_2m"]}%");
            sb.AppendLine($"- **Wind:** {current["wind_speed_10m"]} km/h at {current["wind_direction_10m"]}\u00b0");
            sb.AppendLine($"- **Precipitation:** {current["precipitation"]} mm");
            sb.AppendLine($"- **Observed at:** {current["time"]} ({data?["timezone"]})");
            return sb.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            return Error(DescribeException(ex));
        }
    }

    [McpServerTool(
        Name = "weather_get_forecast",
        Title = "Get Multi-Day Forecast",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true)]
    [Description(
        "Get a daily weather forecast for a named location: high/low temperature, " +
        "precipitation totals and probability, max wind speed, and sky condition per day.")]
    public static async Task<string> GetForecast(
        OpenMeteoClient client,
        [Description("Free-text place name, e.g. 'Lahore', 'London', 'Jeddah'.")]
        string location,
        [Description("Number of daily forecast entries to return, including today (1-16).")]
        int days = 5,
        [Description("Temperature unit: 'celsius' or 'fahrenheit'.")]
        string temperatureUnit = "celsius",
        [Description("Output format: 'markdown' for human-readable or 'json' for machine-readable.")]
        ResponseFormat responseFormat = ResponseFormat.Markdown,
        CancellationToken cancellationToken = default)
    {
        var unitError = ValidateUnit(temperatureUnit);
        if (unitError is not null) return Error(unitError);
        days = Math.Clamp(days, 1, 16);

        try
        {
            var loc = await ResolveLocationAsync(client, location, cancellationToken);
            if (loc is null)
            {
                return NotFound(location);
            }

            var data = await client.GetForecastAsync(loc.Latitude, loc.Longitude, days, temperatureUnit, cancellationToken);
            var daily = data?["daily"];
            var dates = daily?["time"]?.AsArray();
            if (dates is null)
            {
                return Error("Weather service returned no forecast data.");
            }

            var unitSymbol = temperatureUnit == "fahrenheit" ? "\u00b0F" : "\u00b0C";
            var dayEntries = new List<object>();
            for (var i = 0; i < dates.Count; i++)
            {
                dayEntries.Add(new
                {
                    date = daily!["time"]![i]?.GetValue<string>(),
                    condition = WeatherCodes.Describe(daily["weather_code"]?[i]?.GetValue<int>()),
                    temp_max = daily["temperature_2m_max"]?[i]?.GetValue<double>(),
                    temp_min = daily["temperature_2m_min"]?[i]?.GetValue<double>(),
                    precipitation_mm = daily["precipitation_sum"]?[i]?.GetValue<double>(),
                    precipitation_probability_percent = daily["precipitation_probability_max"]?[i]?.GetValue<double>(),
                    max_wind_kmh = daily["wind_speed_10m_max"]?[i]?.GetValue<double>()
                });
            }

            if (responseFormat == ResponseFormat.Json)
            {
                return JsonSerializer.Serialize(new
                {
                    location = loc.DisplayName,
                    latitude = loc.Latitude,
                    longitude = loc.Longitude,
                    timezone = data?["timezone"]?.GetValue<string>(),
                    temperature_unit = unitSymbol,
                    days = dayEntries
                }, JsonOptions);
            }

            var sb = new StringBuilder();
            sb.AppendLine($"## {days}-day forecast for {loc.DisplayName}");
            sb.AppendLine();
            for (var i = 0; i < dates.Count; i++)
            {
                var date = daily!["time"]![i]?.GetValue<string>();
                var condition = WeatherCodes.Describe(daily["weather_code"]?[i]?.GetValue<int>());
                var tMax = daily["temperature_2m_max"]?[i];
                var tMin = daily["temperature_2m_min"]?[i];
                var precip = daily["precipitation_sum"]?[i];
                var precipProb = daily["precipitation_probability_max"]?[i];
                var wind = daily["wind_speed_10m_max"]?[i];
                sb.AppendLine(
                    $"**{date}** \u2014 {condition}: {tMin}{unitSymbol} to {tMax}{unitSymbol}, " +
                    $"precip {precip} mm ({precipProb}% chance), wind up to {wind} km/h");
            }
            return sb.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            return Error(DescribeException(ex));
        }
    }

    [McpServerTool(
        Name = "weather_get_air_quality",
        Title = "Get Air Quality",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true)]
    [Description(
        "Get current air quality for a named location: US AQI, European AQI, PM2.5, PM10, " +
        "and other pollutant concentrations.")]
    public static async Task<string> GetAirQuality(
        OpenMeteoClient client,
        [Description("Free-text place name, e.g. 'Lahore', 'Beijing'.")]
        string location,
        [Description("Output format: 'markdown' for human-readable or 'json' for machine-readable.")]
        ResponseFormat responseFormat = ResponseFormat.Markdown,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var loc = await ResolveLocationAsync(client, location, cancellationToken);
            if (loc is null)
            {
                return NotFound(location);
            }

            var data = await client.GetAirQualityAsync(loc.Latitude, loc.Longitude, cancellationToken);
            var current = data?["current"];
            if (current is null)
            {
                return Error("Air quality service returned no data.");
            }

            if (responseFormat == ResponseFormat.Json)
            {
                return JsonSerializer.Serialize(new
                {
                    location = loc.DisplayName,
                    latitude = loc.Latitude,
                    longitude = loc.Longitude,
                    observed_at = current["time"]?.GetValue<string>(),
                    us_aqi = current["us_aqi"]?.GetValue<double>(),
                    european_aqi = current["european_aqi"]?.GetValue<double>(),
                    pm2_5 = current["pm2_5"]?.GetValue<double>(),
                    pm10 = current["pm10"]?.GetValue<double>(),
                    carbon_monoxide = current["carbon_monoxide"]?.GetValue<double>(),
                    ozone = current["ozone"]?.GetValue<double>(),
                    nitrogen_dioxide = current["nitrogen_dioxide"]?.GetValue<double>()
                }, JsonOptions);
            }

            var sb = new StringBuilder();
            sb.AppendLine($"## Air quality in {loc.DisplayName}");
            sb.AppendLine($"- **US AQI:** {current["us_aqi"]}");
            sb.AppendLine($"- **European AQI:** {current["european_aqi"]}");
            sb.AppendLine($"- **PM2.5:** {current["pm2_5"]} \u00b5g/m\u00b3");
            sb.AppendLine($"- **PM10:** {current["pm10"]} \u00b5g/m\u00b3");
            sb.AppendLine($"- **CO:** {current["carbon_monoxide"]} \u00b5g/m\u00b3");
            sb.AppendLine($"- **O3:** {current["ozone"]} \u00b5g/m\u00b3");
            sb.AppendLine($"- **NO2:** {current["nitrogen_dioxide"]} \u00b5g/m\u00b3");
            sb.AppendLine($"- **Observed at:** {current["time"]}");
            return sb.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            return Error(DescribeException(ex));
        }
    }

    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static async Task<GeoLocation?> ResolveLocationAsync(
        OpenMeteoClient client, string location, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return null;
        }
        var matches = await client.GeocodeAsync(location, 1, ct);
        return matches.Count > 0 ? matches[0] : null;
    }

    private static string? ValidateUnit(string unit) =>
        unit is "celsius" or "fahrenheit"
            ? null
            : "Error: temperatureUnit must be 'celsius' or 'fahrenheit'.";

    private static string NotFound(string location) =>
        $"Error: No location found matching '{location}'. Try a more specific name (e.g. add country or region).";

    private static string Error(string message) => $"Error: {message}";

    private static string DescribeException(Exception ex) => ex switch
    {
        HttpRequestException { StatusCode: HttpStatusCode.NotFound } =>
            "Resource not found.",
        HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests } =>
            "Rate limit exceeded. Please wait before making more requests.",
        HttpRequestException http =>
            $"Weather API request failed ({(int?)http.StatusCode}): {http.Message}",
        TaskCanceledException =>
            "Request to the weather service timed out. Please try again.",
        _ => $"Unexpected error: {ex.GetType().Name}: {ex.Message}"
    };
}
