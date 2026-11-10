using Expresso.Api.Data;
using Expresso.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Expresso.Api.Features.Orders;

public class OrderService(ExpressoDbContext db, TimeProvider clock, ILogger<OrderService> logger)
{
    public async Task<IReadOnlyList<OrderResponse>> ListAsync(OrderStatus? status, string? customer, CancellationToken ct)
    {
        var query = db.Orders.AsNoTracking().Include(o => o.Items).AsQueryable();
        if (status is not null)
            query = query.Where(o => o.Status == status);
        if (!string.IsNullOrWhiteSpace(customer))
            query = query.Where(o => EF.Functions.ILike(o.CustomerName, $"%{customer.Trim()}%"));

        var orders = await query.OrderByDescending(o => o.CreatedAt).Take(100).ToListAsync(ct);
        return orders.Select(OrderResponse.From).ToList();
    }

    public async Task<OrderResponse> GetAsync(int id, CancellationToken ct) =>
        OrderResponse.From(await FindAsync(id, ct));

    public async Task<IReadOnlyList<OrderHistoryEntry>> GetHistoryAsync(int id, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().Include(o => o.History).FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new NotFoundException("Pedido", id);

        return order.History.OrderBy(h => h.ChangedAt).ThenBy(h => h.Id)
            .Select(h => new OrderHistoryEntry(h.From, h.To, h.ChangedAt, h.ChangedBy))
            .ToList();
    }

    public async Task<OrderResponse> CreateAsync(OrderRequest request, string user, CancellationToken ct)
    {
        // One query for all products in the order instead of one per item.
        var ids = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

        var lines = request.Items.Select(i => new OrderLine(
            products.GetValueOrDefault(i.ProductId)
                ?? throw new BusinessRuleException($"O produto {i.ProductId} não existe no cardápio."),
            i.Quantity,
            i.Notes)).ToList();

        var order = Order.Place(request.CustomerName, clock.GetUtcNow(), lines, user);
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Order {OrderId} placed by {User} for {Customer}, total {Total}",
            order.Id, user, order.CustomerName, order.Total);
        return OrderResponse.From(order);
    }

    public Task<OrderResponse> AdvanceStatusAsync(int id, OrderStatus status, string user, CancellationToken ct)
    {
        // Cancelling has its own endpoint because a different role (the attendant) is allowed to do it.
        if (status == OrderStatus.Canceled)
            throw new BusinessRuleException($"Para cancelar, use POST /api/orders/{id}/cancel.");

        return ChangeStatusAsync(id, status, user, ct);
    }

    public Task<OrderResponse> CancelAsync(int id, string user, CancellationToken ct) =>
        ChangeStatusAsync(id, OrderStatus.Canceled, user, ct);

    private async Task<OrderResponse> ChangeStatusAsync(int id, OrderStatus status, string user, CancellationToken ct)
    {
        var order = await FindAsync(id, ct);
        var previous = order.Status;
        order.ChangeStatus(status, clock.GetUtcNow(), user);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Order {OrderId} changed from {From} to {To} by {User}", id, previous, status, user);
        return OrderResponse.From(order);
    }

    private async Task<Order> FindAsync(int id, CancellationToken ct) =>
        await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new NotFoundException("Pedido", id);
}
