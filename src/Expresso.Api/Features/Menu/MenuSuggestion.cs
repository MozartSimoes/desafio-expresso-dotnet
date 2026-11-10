using System.Globalization;
using Expresso.Api.Domain;

namespace Expresso.Api.Features.Menu;

public record Suggestion(string Message, IReadOnlyList<ProductCategory> Categories);

/// <summary>Which part of the menu to highlight for the current temperature. Pure logic, no I/O.</summary>
public static class MenuSuggestion
{
    public const double HotDayFromCelsius = 25;

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static Suggestion For(double? temperatureC)
    {
        if (temperatureC is not { } t)
            return new("Confira os destaques da casa.", [ProductCategory.Coffee, ProductCategory.Snack]);

        var degrees = t.ToString("0.#", PtBr);
        return t >= HotDayFromCelsius
            ? new($"Está {degrees} °C: que tal algo gelado?", [ProductCategory.ColdDrink])
            : new($"Está {degrees} °C: um café ou chá quentinho cai bem.", [ProductCategory.Coffee, ProductCategory.Tea]);
    }
}
