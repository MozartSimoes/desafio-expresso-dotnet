using Expresso.Api.Data;
using Expresso.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Expresso.Api.Features.Receipts;

public class FiscalReceiptOptions
{
    public const string Section = "FiscalReceipts";

    /// <summary>How often the worker looks for receipts to issue.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>Longest the worker waits for the emitter before counting the attempt as failed.</summary>
    public TimeSpan EmitTimeout { get; set; } = TimeSpan.FromSeconds(5);

    public int MaxAttempts { get; set; } = 5;

    /// <summary>Wait before the 2nd attempt; it doubles on each failure (5s, 10s, 20s, 40s...).</summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(5);
}

/// <summary>
/// Issues the pending receipts in the background (outbox pattern): the delivery only records "a receipt is due"
/// in the same transaction, and this worker talks to the slow emitter on its own time.
/// </summary>
public class FiscalReceiptWorker(
    IServiceScopeFactory scopes,
    IOptions<FiscalReceiptOptions> options,
    TimeProvider clock,
    ILogger<FiscalReceiptWorker> logger) : BackgroundService
{
    private readonly FiscalReceiptOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.PollInterval, clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                while (await IssueNextAsync(stoppingToken)) { }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // e.g. the database is restarting: log and try again on the next tick, never stop the worker.
                logger.LogError(ex, "Fiscal receipt worker failed; retrying on the next tick");
            }
        }
    }

    /// <summary>Takes one due receipt and tries to issue it. Returns false when there is nothing to do.</summary>
    private async Task<bool> IssueNextAsync(CancellationToken stoppingToken)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ExpressoDbContext>();
        var emitter = scope.ServiceProvider.GetRequiredService<IFiscalEmitter>();

        await using var transaction = await db.Database.BeginTransactionAsync(stoppingToken);

        // FOR UPDATE SKIP LOCKED: with several API instances running, each receipt is taken by only one of them,
        // and the others skip it instead of waiting. The lock lasts until the attempt is saved.
        var now = clock.GetUtcNow();
        var receipt = (await db.FiscalReceipts
            .FromSql($"""
                SELECT * FROM "FiscalReceipts"
                WHERE "Status" = 'Pending' AND "NextAttemptAt" <= {now}
                ORDER BY "NextAttemptAt"
                LIMIT 1
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(stoppingToken)).FirstOrDefault();

        if (receipt is null)
            return false;

        var order = await db.Orders.AsNoTracking().Include(o => o.Items)
            .FirstAsync(o => o.Id == receipt.OrderId, stoppingToken);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(_options.EmitTimeout);
        try
        {
            var number = await emitter.EmitAsync(receipt.Id, order, timeout.Token);
            receipt.MarkIssued(number, clock.GetUtcNow());
            logger.LogInformation("Receipt {Number} issued for order {OrderId} on attempt {Attempt}",
                number, order.Id, receipt.Attempts);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            var reason = ex is OperationCanceledException
                ? $"O emissor não respondeu em {_options.EmitTimeout.TotalSeconds:0.#}s."
                : ex.Message;
            receipt.RegisterFailure(reason, clock.GetUtcNow(), _options.MaxAttempts, _options.RetryBaseDelay);

            if (receipt.Status == FiscalReceiptStatus.Failed)
                logger.LogError("Receipt for order {OrderId} gave up after {Attempts} attempts: {Reason}",
                    order.Id, receipt.Attempts, reason);
            else
                logger.LogWarning("Receipt for order {OrderId} failed (attempt {Attempt}), next try at {NextAttemptAt}: {Reason}",
                    order.Id, receipt.Attempts, receipt.NextAttemptAt, reason);
        }

        await db.SaveChangesAsync(stoppingToken);
        await transaction.CommitAsync(stoppingToken);
        return true;
    }
}
