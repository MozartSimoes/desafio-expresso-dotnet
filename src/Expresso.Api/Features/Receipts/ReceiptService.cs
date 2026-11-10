using Expresso.Api.Data;
using Expresso.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Expresso.Api.Features.Receipts;

public record FiscalReceiptResponse(
    int OrderId,
    FiscalReceiptStatus Status,
    string? Number,
    int Attempts,
    DateTimeOffset CreatedAt,
    DateTimeOffset? IssuedAt,
    DateTimeOffset? NextAttemptAt,
    string? LastError)
{
    public static FiscalReceiptResponse From(FiscalReceipt r) => new(
        r.OrderId, r.Status, r.Number, r.Attempts, r.CreatedAt, r.IssuedAt,
        r.Status == FiscalReceiptStatus.Pending ? r.NextAttemptAt : null,
        r.LastError);
}

public class ReceiptService(ExpressoDbContext db, TimeProvider clock, ILogger<ReceiptService> logger)
{
    public async Task<FiscalReceiptResponse> GetAsync(int orderId, CancellationToken ct) =>
        FiscalReceiptResponse.From(await FindAsync(orderId, db.FiscalReceipts.AsNoTracking(), ct));

    public async Task<FiscalReceiptResponse> RetryAsync(int orderId, string user, CancellationToken ct)
    {
        var receipt = await FindAsync(orderId, db.FiscalReceipts, ct);
        receipt.Retry(clock.GetUtcNow());
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Receipt for order {OrderId} resent by {User}", orderId, user);
        return FiscalReceiptResponse.From(receipt);
    }

    private async Task<FiscalReceipt> FindAsync(int orderId, IQueryable<FiscalReceipt> receipts, CancellationToken ct)
    {
        var receipt = await receipts.FirstOrDefaultAsync(r => r.OrderId == orderId, ct);
        if (receipt is not null)
            return receipt;

        if (!await db.Orders.AnyAsync(o => o.Id == orderId, ct))
            throw new NotFoundException("Pedido", orderId);
        throw new ConflictException("O cupom fiscal só é gerado quando o pedido é entregue.");
    }
}
