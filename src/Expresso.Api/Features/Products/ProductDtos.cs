using System.ComponentModel.DataAnnotations;
using Expresso.Api.Domain;

namespace Expresso.Api.Features.Products;

public record ProductRequest(
    [Required(ErrorMessage = "Informe o nome.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "O nome deve ter entre 2 e 80 caracteres.")]
    string Name,
    [Required(ErrorMessage = "Informe a categoria.")] ProductCategory? Category,
    ProductSize? Size,
    [Range(0.01, 9999.99, ErrorMessage = "O preço deve estar entre 0,01 e 9.999,99.")] decimal Price,
    bool Available = true,
    [Range(0, 100_000, ErrorMessage = "O estoque deve estar entre 0 e 100.000.")] int? Stock = null);

public static class ProductOrdering
{
    // Sorted in memory: sizes are stored as text, and the database would put "G" before "M" and "P".
    // The menu is small, so this costs nothing.
    public static IEnumerable<Product> OrderForMenu(this IEnumerable<Product> products) =>
        products.OrderBy(p => p.Category).ThenBy(p => p.Name).ThenBy(p => p.Size);
}

public record ProductResponse(
    int Id, string Name, ProductCategory Category, ProductSize? Size, decimal Price, bool Available, int? Stock)
{
    public static ProductResponse From(Product p) =>
        new(p.Id, p.Name, p.Category, p.Size, p.Price, p.Available, p.Stock);
}
