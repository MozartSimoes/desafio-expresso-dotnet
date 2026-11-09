namespace Expresso.Api.Domain;

/// <summary>
/// A line of an order. Name, size and unit price are copied from the product when the order is placed,
/// so later menu changes (price, name) never rewrite an order that was already registered.
/// </summary>
public class OrderItem
{
    public int Id { get; private set; }
    public int ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public ProductSize? Size { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public string? Notes { get; private set; }

    public decimal Subtotal => UnitPrice * Quantity;

    private OrderItem() { } // EF Core

    internal OrderItem(Product product, int quantity, string? notes)
    {
        ProductId = product.Id;
        ProductName = product.Name;
        Size = product.Size;
        UnitPrice = product.Price;
        Quantity = quantity;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }
}
