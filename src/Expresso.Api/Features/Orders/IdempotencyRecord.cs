using Expresso.Api.Domain;

namespace Expresso.Api.Features.Orders;

/// <summary>
/// "Toque duplo": remembers which order an Idempotency-Key created, so a repeated "Finalizar" from the totem
/// returns that same order instead of placing a second one. The key is the primary key, so the database itself
/// guarantees a key is used once, even when both taps arrive at the same instant.
/// </summary>
public class IdempotencyRecord
{
    public string Key { get; private set; } = string.Empty;

    /// <summary>Hash of the request body: the same key with a different order is a client bug, not a retry.</summary>
    public string RequestHash { get; private set; } = string.Empty;

    public int OrderId { get; private set; }
    public Order Order { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    private IdempotencyRecord() { } // EF Core

    public IdempotencyRecord(string key, string requestHash, Order order, DateTimeOffset createdAt)
    {
        Key = key;
        RequestHash = requestHash;
        Order = order;
        CreatedAt = createdAt;
    }
}
