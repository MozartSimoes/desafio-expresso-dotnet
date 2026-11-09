namespace Expresso.Api.Domain;

/// <summary>What the customer asked for: one product, how many, and an optional note ("sem açúcar").</summary>
public record OrderLine(Product Product, int Quantity, string? Notes);

public class Order
{
    private readonly List<OrderItem> _items = [];

    public int Id { get; private set; }
    public string CustomerName { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public OrderStatus Status { get; private set; }

    /// <summary>Always calculated here from the items; never accepted from the client.</summary>
    public decimal Total { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    private Order() { } // EF Core

    public static Order Place(string customerName, DateTimeOffset now, IEnumerable<OrderLine> lines)
    {
        var order = new Order
        {
            CustomerName = customerName.Trim(),
            CreatedAt = now,
            Status = OrderStatus.Received
        };

        foreach (var line in lines)
            order._items.Add(new OrderItem(line.Product, line.Quantity, line.Notes));

        order.Total = order._items.Sum(i => i.Subtotal);
        return order;
    }
}
