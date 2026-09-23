using System.Text.Json;
using System.Text.Json.Nodes;

namespace WeatherMcp;

public sealed record GeoLocation(
    string Name,
    string? Country,
    string? Admin1,
    double Latitude,
    double Longitude,
    string? Timezone,
    long? Population)
{
    public string DisplayName
    {
        get
        {
            var parts = new List<string> { Name };
            if (!string.IsNullOrWhiteSpace(Admin1)) parts.Add(Admin1!);
            if (!string.IsNullOrWhiteSpace(Country)) parts.Add(Country!);
            return string.Join(", ", parts);
        }
    }
}

/// <summary>
/// Thin async wrapper around the free Open-Meteo geocoding, forecast, and
/// air-quality endpoints. No API key required.
/// </summary>
public sealed class OpenMeteoClient(HttpClient httpClient)
{
    private const string GeocodingUrl = "https://geocoding-api.open-meteo.com/v1/search";
    private const string ForecastUrl = "https://api.open-meteo.com/v1/forecast";
    private const string AirQualityUrl = "https://air-quality-api.open-meteo.com/v1/air-quality";

    public async Task<List<GeoLocation>> GeocodeAsync(string location, int count, CancellationToken ct)
    {
        var url = $"{GeocodingUrl}?name={Uri.EscapeDataString(location)}&count={count}&language=en&format=json";
        using var response = await httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var json = await JsonNode.ParseAsync(stream, cancellationToken: ct);
        var results = json?["results"]?.AsArray();
        if (results is null)
        {
            return [];
        }

        var locations = new List<GeoLocation>();
        foreach (var node in results)
        {
            if (node is null) continue;
            locations.Add(new GeoLocation(
                Name: node["name"]?.GetValue<string>() ?? "",
                Country: node["country"]?.GetValue<string>(),
                Admin1: node["admin1"]?.GetValue<string>(),
                Latitude: node["latitude"]!.GetValue<double>(),
                Longitude: node["longitude"]!.GetValue<double>(),
                Timezone: node["timezone"]?.GetValue<string>(),
                Population: node["population"]?.GetValue<long>()));
        }

        return locations;
    }

    public async Task<JsonNode?> GetCurrentWeatherAsync(
        double lat, double lon, string temperatureUnit, CancellationToken ct)
    {
        var fields = "temperature_2m,apparent_temperature,relative_humidity_2m,precipitation,weather_code,wind_speed_10m,wind_direction_10m";
        var url = $"{ForecastUrl}?latitude={lat}&longitude={lon}&current={fields}" +
                   $"&temperature_unit={temperatureUnit}&wind_speed_unit=kmh&precipitation_unit=mm&timezone=auto";
        return await GetJsonAsync(url, ct);
    }

    public async Task<JsonNode?> GetForecastAsync(
        double lat, double lon, int days, string temperatureUnit, CancellationToken ct)
    {
        var fields = "weather_code,temperature_2m_max,temperature_2m_min,precipitation_sum,precipitation_probability_max,wind_speed_10m_max";
        var url = $"{ForecastUrl}?latitude={lat}&longitude={lon}&daily={fields}" +
                   $"&temperature_unit={temperatureUnit}&wind_speed_unit=kmh&precipitation_unit=mm" +
                   $"&forecast_days={days}&timezone=auto";
        return await GetJsonAsync(url, ct);
    }

    public async Task<JsonNode?> GetAirQualityAsync(double lat, double lon, CancellationToken ct)
    {
        var fields = "us_aqi,european_aqi,pm2_5,pm10,carbon_monoxide,ozone,nitrogen_dioxide";
        var url = $"{AirQualityUrl}?latitude={lat}&longitude={lon}&current={fields}&timezone=auto";
        return await GetJsonAsync(url, ct);
    }

    private async Task<JsonNode?> GetJsonAsync(string url, CancellationToken ct)
    {
        using var response = await httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonNode.ParseAsync(stream, cancellationToken: ct);
    }
}
