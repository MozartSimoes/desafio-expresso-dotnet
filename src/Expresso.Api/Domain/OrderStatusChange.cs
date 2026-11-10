namespace Expresso.Api.Domain;

/// <summary>
/// One step in the life of an order: who moved it, when, and from which status to which.
/// Answers "meu pedido sumiu": the team can see exactly what happened to it.
/// </summary>
public class OrderStatusChange
{
    public int Id { get; private set; }

    /// <summary>Null for the first entry, when the order is placed.</summary>
    public OrderStatus? From { get; private set; }

    public OrderStatus To { get; private set; }
    public DateTimeOffset ChangedAt { get; private set; }
    public string ChangedBy { get; private set; } = string.Empty;

    private OrderStatusChange() { } // EF Core

    internal OrderStatusChange(OrderStatus? from, OrderStatus to, DateTimeOffset changedAt, string changedBy)
    {
        From = from;
        To = to;
        ChangedAt = changedAt;
        ChangedBy = changedBy;
    }
}
