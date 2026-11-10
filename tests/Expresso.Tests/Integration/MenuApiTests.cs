using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Expresso.Api.Domain;
using Expresso.Api.Features.Menu;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Expresso.Tests.Integration;

[Collection(ApiCollection.Name)]
public class MenuApiTests : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public MenuApiTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        ClearCache(); // each test sets its own external answers
    }

    [Fact]
    public async Task Hot_day_suggests_cold_drinks()
    {
        _factory.ExternalApis.Weather = new CurrentWeather(31, "céu limpo");

        var menu = await GetMenuAsync();

        Assert.Equal(31, menu.Weather!.TemperatureC);
        Assert.NotEmpty(menu.SuggestedProducts);
        Assert.All(menu.SuggestedProducts, p => Assert.Equal(ProductCategory.ColdDrink, p.Category));
        Assert.Empty(menu.Warnings);
    }

    [Fact]
    public async Task Weather_api_down_still_returns_the_menu_with_a_warning()
    {
        _factory.ExternalApis.Weather = null;

        var menu = await GetMenuAsync();

        Assert.Null(menu.Weather);
        Assert.NotEmpty(menu.SuggestedProducts);
        Assert.Contains(menu.Warnings, w => w.Contains("clima"));
    }

    [Fact]
    public async Task Holiday_shows_special_opening_hours()
    {
        var today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);
        _factory.ExternalApis.Holidays = [new Holiday(today, "Feriado de teste")];

        var menu = await GetMenuAsync();

        Assert.Equal("Feriado de teste", menu.Holiday);
        Assert.StartsWith("Feriado", menu.OpeningHours);
    }

    [Fact]
    public async Task Holiday_api_down_falls_back_to_regular_hours()
    {
        _factory.ExternalApis.Holidays = null;

        var menu = await GetMenuAsync();

        Assert.Null(menu.Holiday);
        Assert.Equal("07:00 às 20:00", menu.OpeningHours);
        Assert.Contains(menu.Warnings, w => w.Contains("feriados"));
    }

    public void Dispose()
    {
        _factory.ExternalApis.Weather = new CurrentWeather(22, "céu limpo");
        _factory.ExternalApis.Holidays = [];
    }

    private async Task<MenuTodayResponse> GetMenuAsync() =>
        (await _client.GetFromJsonAsync<MenuTodayResponse>("/api/menu/today", Json))!;

    private void ClearCache() => ((MemoryCache)_factory.Services.GetRequiredService<IMemoryCache>()).Clear();
}
