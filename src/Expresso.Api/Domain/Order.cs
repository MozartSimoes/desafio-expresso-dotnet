namespace Expresso.Api.Domain;

/// <summary>What the customer asked for: one product, how many, and an optional note ("sem açúcar").</summary>
public record OrderLine(Product Product, int Quantity, string? Notes);

public class Order
{
    // The whole lifecycle in one table: Received → Preparing → Ready → Delivered, and cancel only while Received.
    private static readonly Dictionary<OrderStatus, OrderStatus[]> AllowedTransitions = new()
    {
        [OrderStatus.Received] = [OrderStatus.Preparing, OrderStatus.Canceled],
        [OrderStatus.Preparing] = [OrderStatus.Ready],
        [OrderStatus.Ready] = [OrderStatus.Delivered],
        [OrderStatus.Delivered] = [],
        [OrderStatus.Canceled] = []
    };

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
            order.AddItem(line);

        if (order._items.Count == 0)
            throw new BusinessRuleException("O pedido precisa ter pelo menos um item.");

        order.Total = order._items.Sum(i => i.Subtotal);
        return order;
    }

    public void ChangeStatus(OrderStatus next)
    {
        if (AllowedTransitions[Status].Contains(next))
        {
            Status = next;
            return;
        }

        throw new ConflictException(next == OrderStatus.Canceled
            ? $"O pedido só pode ser cancelado enquanto estiver Recebido (situação atual: {Status.ToLabel()})."
            : $"Não é possível mudar a situação de {Status.ToLabel()} para {next.ToLabel()}.");
    }

    private void AddItem(OrderLine line)
    {
        if (line.Quantity <= 0)
            throw new BusinessRuleException($"A quantidade de '{line.Product.Name}' deve ser maior que zero.");
        if (!line.Product.Available)
            throw new BusinessRuleException($"O produto '{line.Product.Name}' está indisponível no momento.");

        _items.Add(new OrderItem(line.Product, line.Quantity, line.Notes));
    }
}
