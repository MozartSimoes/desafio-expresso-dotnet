using Expresso.Api.Domain;
using Expresso.Api.Features.Menu;

namespace Expresso.Tests.Menu;

public class MenuSuggestionTests
{
    [Theory]
    [InlineData(25.0)] // boundary: 25 °C already counts as a hot day
    [InlineData(32.5)]
    public void Hot_day_suggests_cold_drinks(double temperature)
    {
        var suggestion = MenuSuggestion.For(temperature);

        Assert.Equal([ProductCategory.ColdDrink], suggestion.Categories);
    }

    [Theory]
    [InlineData(24.9)]
    [InlineData(12.0)]
    public void Mild_or_cold_day_suggests_hot_drinks(double temperature)
    {
        var suggestion = MenuSuggestion.For(temperature);

        Assert.Equal([ProductCategory.Coffee, ProductCategory.Tea], suggestion.Categories);
    }

    [Fact]
    public void Message_uses_brazilian_number_format()
    {
        Assert.StartsWith("Está 24,5 °C", MenuSuggestion.For(24.5).Message);
    }

    [Fact]
    public void Without_weather_suggests_house_highlights()
    {
        var suggestion = MenuSuggestion.For(null);

        Assert.Contains(ProductCategory.Coffee, suggestion.Categories);
    }
}
