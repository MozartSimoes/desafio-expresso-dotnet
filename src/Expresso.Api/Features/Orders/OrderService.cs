using System.Security.Cryptography;
using System.Text.Json;
using Expresso.Api.Data;
using Expresso.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Expresso.Api.Features.Orders;

public record CreateOrderResult(OrderResponse Order, bool Replayed);

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

    public async Task<CreateOrderResult> CreateAsync(
        OrderRequest request, string? idempotencyKey, string user, CancellationToken ct)
    {
        var requestHash = HashOf(request);
        if (idempotencyKey is not null && await FindReplayAsync(idempotencyKey, requestHash, ct) is { } replay)
            return replay;

        // One query for all products in the order instead of one per item.
        var ids = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

        var lines = request.Items.Select(i => new OrderLine(
            products.GetValueOrDefault(i.ProductId)
                ?? throw new BusinessRuleException($"O produto {i.ProductId} não existe no cardápio."),
            i.Quantity,
            i.Notes)).ToList();

        var order = Order.Place(request.CustomerName, clock.GetUtcNow(), lines, user);

        // Stock and order are saved together: if any item runs out, nothing is saved.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await ReserveStockAsync(order, ct);
        db.Orders.Add(order);
        if (idempotencyKey is not null)
            db.IdempotencyRecords.Add(new IdempotencyRecord(idempotencyKey, requestHash, order, clock.GetUtcNow()));

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (idempotencyKey is not null && IsDuplicateKey(ex))
        {
            // Both taps arrived at the same instant and the other one saved first: undo this one
            // (including its stock reservation) and answer with the order the first tap created.
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            var winner = await FindReplayAsync(idempotencyKey, requestHash, ct);
            if (winner is null)
                throw;
            return winner;
        }
        await transaction.CommitAsync(ct);

        logger.LogInformation("Order {OrderId} placed by {User} for {Customer}, total {Total}",
            order.Id, user, order.CustomerName, order.Total);
        return new CreateOrderResult(OrderResponse.From(order), Replayed: false);
    }

    private async Task<CreateOrderResult?> FindReplayAsync(string key, string requestHash, CancellationToken ct)
    {
        var record = await db.IdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(r => r.Key == key, ct);
        if (record is null)
            return null;
        if (record.RequestHash != requestHash)
            throw new ConflictException("Esta Idempotency-Key já foi usada em um pedido diferente.");

        logger.LogInformation("Repeated request with key {Key} answered with order {OrderId}", key, record.OrderId);
        return new CreateOrderResult(OrderResponse.From(await FindAsync(record.OrderId, ct)), Replayed: true);
    }

    private static string HashOf(OrderRequest request) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request)));

    private static bool IsDuplicateKey(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg &&
        pg.ConstraintName == "PK_IdempotencyRecords";

    public Task<OrderResponse> AdvanceStatusAsync(int id, OrderStatus status, string user, CancellationToken ct)
    {
        // Cancelling has its own endpoint because a different role (the attendant) is allowed to do it.
        if (status == OrderStatus.Canceled)
            throw new BusinessRuleException($"Para cancelar, use POST /api/orders/{id}/cancel.");

        return ChangeStatusAsync(id, status, user, ct);
    }

    public async Task<OrderResponse> CancelAsync(int id, string user, CancellationToken ct)
    {
        // The cancellation and the stock going back to the shelf happen together.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var response = await ChangeStatusAsync(id, OrderStatus.Canceled, user, ct);
        foreach (var (productId, quantity) in QuantitiesByProduct(response.Items.Select(i => (i.ProductId, i.Quantity))))
        {
            await db.Products
                .Where(p => p.Id == productId && p.Stock != null)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock + quantity), ct);
        }
        await transaction.CommitAsync(ct);
        return response;
    }

    /// <summary>
    /// "Pico das 8h": several attendants may sell the last pão de queijo at the same moment. Instead of
    /// read-check-write in C# (where two requests both see 1 left), the database does it in one atomic
    /// statement: UPDATE ... SET stock = stock - n WHERE id = @id AND stock >= n. If no row changed, it ran out.
    /// </summary>
    private async Task ReserveStockAsync(Order order, CancellationToken ct)
    {
        var ids = order.Items.Select(i => i.ProductId).Distinct().ToList();
        var limited = await db.Products
            .Where(p => ids.Contains(p.Id) && p.Stock != null)
            .Select(p => new { p.Id, p.Name })
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        // Always lock products in the same order (by id), so two orders can never wait on each other (deadlock).
        foreach (var (productId, quantity) in QuantitiesByProduct(order.Items.Select(i => (i.ProductId, i.Quantity)))
                     .Where(q => limited.ContainsKey(q.ProductId)))
        {
            var updated = await db.Products
                .Where(p => p.Id == productId && p.Stock >= quantity)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - quantity), ct);

            if (updated == 0)
                throw new ConflictException($"Estoque insuficiente de '{limited[productId]}' para {quantity} unidade(s).");
        }
    }

    // The same product can appear in two lines ("sem açúcar" and "com açúcar"); stock counts the total.
    private static IEnumerable<(int ProductId, int Quantity)> QuantitiesByProduct(
        IEnumerable<(int ProductId, int Quantity)> items) =>
        items.GroupBy(i => i.ProductId)
            .Select(g => (g.Key, g.Sum(i => i.Quantity)))
            .OrderBy(q => q.Key);

    private async Task<OrderResponse> ChangeStatusAsync(int id, OrderStatus status, string user, CancellationToken ct)
    {
        var order = await FindAsync(id, ct);
        var previous = order.Status;
        var now = clock.GetUtcNow();
        order.ChangeStatus(status, now, user);

        // Outbox: the delivery only records that a receipt is due, in the same save. The slow emitter is
        // called later by FiscalReceiptWorker, so the counter never waits for it (or fails because of it).
        if (status == OrderStatus.Delivered)
            db.FiscalReceipts.Add(FiscalReceipt.For(order, now));

        await db.SaveChangesAsync(ct);

        logger.LogInformation("Order {OrderId} changed from {From} to {To} by {User}", id, previous, status, user);
        return OrderResponse.From(order);
    }

    private async Task<Order> FindAsync(int id, CancellationToken ct) =>
        await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new NotFoundException("Pedido", id);
}
