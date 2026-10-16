using System.Text.Json;
using RaceDay.Api.Dtos;

namespace RaceDay.Api.Services;

public interface IWeatherService
{
    /// <summary>Gets the weather forecast for a location on a given date.</summary>
    Task<WeatherDto> GetForecastAsync(string location, DateOnly date, CancellationToken cancellationToken = default);
}

/// <summary>Live weather from Open-Meteo (free, no API key). Daily forecasts cover the next 16 days.</summary>
public class OpenMeteoWeatherService : IWeatherService
{
    private readonly HttpClient _http;

    public OpenMeteoWeatherService(HttpClient http) => _http = http;

    public async Task<WeatherDto> GetForecastAsync(string location, DateOnly date, CancellationToken cancellationToken = default)
    {
        // "Johannesburg, Gauteng" -> search for "Johannesburg"
        var place = location.Split(',')[0].Trim();
        var geoUrl = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(place)}&count=1&language=en&format=json";

        using var geoStream = await _http.GetStreamAsync(geoUrl, cancellationToken);
        using var geoDoc = await JsonDocument.ParseAsync(geoStream, cancellationToken: cancellationToken);

        if (!geoDoc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
            return new WeatherDto(location, date, false, null, null, null, null, null, "The event location could not be found by the weather provider.");

        var latitude = results[0].GetProperty("latitude").GetDouble();
        var longitude = results[0].GetProperty("longitude").GetDouble();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (date < today || date > today.AddDays(15))
            return new WeatherDto(location, date, false, null, null, null, null, null,
                "A daily forecast is only available for the next 16 days. Check again closer to race day.");

        // Invariant culture: South African regional settings use a comma as the decimal separator.
        var forecastUrl = FormattableString.Invariant(
            $"https://api.open-meteo.com/v1/forecast?latitude={latitude}&longitude={longitude}" +
            $"&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max,wind_speed_10m_max" +
            $"&timezone=auto&start_date={date:yyyy-MM-dd}&end_date={date:yyyy-MM-dd}");

        using var stream = await _http.GetStreamAsync(forecastUrl, cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var daily = doc.RootElement.GetProperty("daily");

        var code = ReadDouble(daily, "weather_code");
        var rain = ReadDouble(daily, "precipitation_probability_max");

        return new WeatherDto(
            location, date, true,
            ReadDouble(daily, "temperature_2m_max"),
            ReadDouble(daily, "temperature_2m_min"),
            rain.HasValue ? (int?)Math.Round(rain.Value) : null,
            ReadDouble(daily, "wind_speed_10m_max"),
            code.HasValue ? Describe((int)code.Value) : null,
            null);
    }

    private static double? ReadDouble(JsonElement daily, string name)
    {
        if (daily.TryGetProperty(name, out var array)
            && array.ValueKind == JsonValueKind.Array
            && array.GetArrayLength() > 0
            && array[0].ValueKind == JsonValueKind.Number)
        {
            return array[0].GetDouble();
        }
        return null;
    }

    // WMO weather interpretation codes -> plain English
    private static string Describe(int code) => code switch
    {
        0 => "Clear sky",
        1 or 2 or 3 => "Partly cloudy",
        45 or 48 => "Fog",
        >= 51 and <= 57 => "Drizzle",
        >= 61 and <= 67 => "Rain",
        >= 71 and <= 77 => "Snow",
        >= 80 and <= 82 => "Rain showers",
        >= 95 => "Thunderstorm",
        _ => "Mixed conditions"
    };
}
