using Expresso.Api.Data;
using Expresso.Api.Features.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Expresso.Api.Features.Menu;

public record WeatherInfo(string City, double TemperatureC, string Condition);

public record MenuTodayResponse(
    DateOnly Date,
    string OpeningHours,
    string? Holiday,
    WeatherInfo? Weather,
    string Suggestion,
    IReadOnlyList<ProductResponse> SuggestedProducts,
    IReadOnlyList<string> Warnings);

/// <summary>
/// The "menu of the day": weather-based suggestions plus holiday opening hours. External APIs are optional:
/// if one is slow or down, the menu still answers, without that piece and with a warning.
/// </summary>
public class MenuService(
    ExpressoDbContext db,
    IWeatherClient weatherClient,
    IHolidayClient holidayClient,
    IMemoryCache cache,
    IOptions<CafeteriaOptions> options,
    TimeProvider clock,
    ILogger<MenuService> logger)
{
    private static readonly TimeSpan WeatherCacheDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan HolidayCacheDuration = TimeSpan.FromHours(12);

    public async Task<MenuTodayResponse> GetTodayAsync(CancellationToken ct)
    {
        var cafeteria = options.Value;
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(cafeteria.TimeZone);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), timeZone).DateTime);
        var warnings = new List<string>();

        // Both calls run in parallel; the cache keeps the external APIs out of every request.
        var weatherTask = TryAsync("o clima", warnings, () =>
            cache.GetOrCreateAsync("weather", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = WeatherCacheDuration;
                return weatherClient.GetCurrentAsync(cafeteria.Latitude, cafeteria.Longitude, ct);
            }), ct);
        var holidaysTask = TryAsync("os feriados", warnings, () =>
            cache.GetOrCreateAsync($"holidays:{today.Year}", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = HolidayCacheDuration;
                return holidayClient.GetNationalHolidaysAsync(today.Year, ct);
            }), ct);

        var weather = await weatherTask;
        var holiday = (await holidaysTask)?.FirstOrDefault(h => h.Date == today);

        var suggestion = MenuSuggestion.For(weather?.TemperatureC);
        var products = await db.Products.AsNoTracking()
            .Where(p => p.Available && suggestion.Categories.Contains(p.Category))
            .ToListAsync(ct);

        return new MenuTodayResponse(
            today,
            holiday is null ? cafeteria.RegularHours : cafeteria.HolidayHours,
            holiday?.Name,
            weather is null ? null : new WeatherInfo(cafeteria.City, weather.TemperatureC, weather.Condition),
            suggestion.Message,
            products.OrderForMenu().Select(ProductResponse.From).ToList(),
            warnings);
    }

    private async Task<T?> TryAsync<T>(string what, List<string> warnings, Func<Task<T?>> call, CancellationToken ct)
        where T : class
    {
        try
        {
            return await call();
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Resilience already retried and timed out; here we only degrade gracefully.
            logger.LogWarning(ex, "Could not fetch {What} from the external API", what);
            lock (warnings)
                warnings.Add($"Não foi possível consultar {what} agora.");
            return null;
        }
    }
}
