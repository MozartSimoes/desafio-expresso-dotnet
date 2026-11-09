using Expresso.Api.Data;
using Expresso.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Expresso.Api.Features.Orders;

public class OrderService(ExpressoDbContext db, TimeProvider clock)
{
    public async Task<IReadOnlyList<OrderResponse>> ListAsync(OrderStatus? status, CancellationToken ct)
    {
        var query = db.Orders.AsNoTracking().Include(o => o.Items).AsQueryable();
        if (status is not null)
            query = query.Where(o => o.Status == status);

        var orders = await query.OrderByDescending(o => o.CreatedAt).Take(100).ToListAsync(ct);
        return orders.Select(OrderResponse.From).ToList();
    }

    public async Task<OrderResponse> GetAsync(int id, CancellationToken ct) =>
        OrderResponse.From(await FindAsync(id, ct));

    public async Task<OrderResponse> CreateAsync(OrderRequest request, CancellationToken ct)
    {
        // One query for all products in the order instead of one per item.
        var ids = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

        var lines = request.Items.Select(i => new OrderLine(
            products.GetValueOrDefault(i.ProductId)
                ?? throw new BusinessRuleException($"O produto {i.ProductId} não existe no cardápio."),
            i.Quantity,
            i.Notes)).ToList();

        var order = Order.Place(request.CustomerName, clock.GetUtcNow(), lines);
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);
        return OrderResponse.From(order);
    }

    private async Task<Order> FindAsync(int id, CancellationToken ct) =>
        await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new NotFoundException("Pedido", id);
}
