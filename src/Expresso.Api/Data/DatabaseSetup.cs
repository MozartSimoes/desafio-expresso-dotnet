using Expresso.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Expresso.Api.Data;

public static class DatabaseSetup
{
    /// <summary>
    /// Applies pending migrations and seeds a sample menu on an empty database, so the API is ready to try
    /// right after <c>docker compose up</c>. In production, migrations would run in the deploy pipeline instead.
    /// </summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ExpressoDbContext>();

        await db.Database.MigrateAsync();

        if (await db.Products.AnyAsync())
            return;

        db.Products.AddRange(
            new Product("Café expresso", ProductCategory.Coffee, ProductSize.P, 6.50m),
            new Product("Cappuccino", ProductCategory.Coffee, ProductSize.P, 9.00m),
            new Product("Cappuccino", ProductCategory.Coffee, ProductSize.M, 11.00m),
            new Product("Cappuccino", ProductCategory.Coffee, ProductSize.G, 13.00m),
            new Product("Latte", ProductCategory.Coffee, ProductSize.M, 12.00m),
            new Product("Chá de camomila", ProductCategory.Tea, ProductSize.M, 7.00m),
            new Product("Café gelado", ProductCategory.ColdDrink, ProductSize.M, 12.00m),
            new Product("Chá mate gelado", ProductCategory.ColdDrink, ProductSize.G, 9.00m),
            new Product("Pão de queijo", ProductCategory.Snack, null, 5.00m, stock: 30),
            new Product("Coxinha", ProductCategory.Snack, null, 7.50m, stock: 20),
            new Product("Bolo de cenoura", ProductCategory.Dessert, null, 8.00m, stock: 8),
            new Product("Croissant", ProductCategory.Snack, null, 9.50m, available: false));

        await db.SaveChangesAsync();
    }
}
