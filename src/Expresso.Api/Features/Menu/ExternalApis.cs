using System.Text.Json.Serialization;

namespace Expresso.Api.Features.Menu;

public record CurrentWeather(double TemperatureC, string Condition);

public record Holiday(DateOnly Date, string Name);

// Interfaces only at the boundary with external services, so tests can replace them without calling the internet.
public interface IWeatherClient
{
    Task<CurrentWeather> GetCurrentAsync(double latitude, double longitude, CancellationToken ct);
}

public interface IHolidayClient
{
    Task<IReadOnlyList<Holiday>> GetNationalHolidaysAsync(int year, CancellationToken ct);
}

/// <summary>Open-Meteo (open-meteo.com): free, no API key.</summary>
public class OpenMeteoClient(HttpClient http) : IWeatherClient
{
    public async Task<CurrentWeather> GetCurrentAsync(double latitude, double longitude, CancellationToken ct)
    {
        var url = FormattableString.Invariant(
            $"v1/forecast?latitude={latitude}&longitude={longitude}&current=temperature_2m,weather_code");
        var response = await http.GetFromJsonAsync<ForecastResponse>(url, ct)
            ?? throw new HttpRequestException("Open-Meteo returned an empty body.");

        return new CurrentWeather(response.Current.Temperature, Describe(response.Current.WeatherCode));
    }

    // WMO weather codes, grouped: https://open-meteo.com/en/docs
    private static string Describe(int code) => code switch
    {
        0 => "céu limpo",
        <= 3 => "parcialmente nublado",
        45 or 48 => "neblina",
        <= 67 => "chuva",
        <= 77 => "neve",
        <= 82 => "pancadas de chuva",
        _ => "tempestade"
    };

    private record ForecastResponse(ForecastCurrent Current);

    private record ForecastCurrent(
        [property: JsonPropertyName("temperature_2m")] double Temperature,
        [property: JsonPropertyName("weather_code")] int WeatherCode);
}

/// <summary>BrasilAPI national holidays (brasilapi.com.br).</summary>
public class BrasilApiHolidayClient(HttpClient http) : IHolidayClient
{
    public async Task<IReadOnlyList<Holiday>> GetNationalHolidaysAsync(int year, CancellationToken ct) =>
        await http.GetFromJsonAsync<List<Holiday>>($"api/feriados/v1/{year}", ct)
            ?? throw new HttpRequestException("BrasilAPI returned an empty body.");
}
