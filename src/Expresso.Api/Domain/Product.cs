namespace Expresso.Api.Domain;

/// <summary>
/// A menu item. Each size is its own product ("Cappuccino P" and "Cappuccino M"), so a size has its own price.
/// </summary>
public class Product
{
    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public ProductCategory Category { get; private set; }

    /// <summary>Null for items sold by the unit, such as snacks.</summary>
    public ProductSize? Size { get; private set; }

    public decimal Price { get; private set; }
    public bool Available { get; private set; }

    /// <summary>
    /// Units left, for items made in limited batches (pão de queijo, coxinha). Null means no stock control:
    /// drinks are prepared on demand. Sales decrement it atomically in the database, never through this property.
    /// </summary>
    public int? Stock { get; private set; }

    private Product() { } // EF Core

    public Product(
        string name, ProductCategory category, ProductSize? size, decimal price, bool available = true, int? stock = null)
    {
        Update(name, category, size, price, available, stock);
    }

    public void Update(
        string name, ProductCategory category, ProductSize? size, decimal price, bool available, int? stock = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new BusinessRuleException("O nome do produto é obrigatório.");
        if (price <= 0)
            throw new BusinessRuleException("O preço deve ser maior que zero.");
        if (stock < 0)
            throw new BusinessRuleException("O estoque não pode ser negativo.");

        Name = name.Trim();
        Category = category;
        Size = size;
        Price = price;
        Available = available;
        Stock = stock;
    }
}
