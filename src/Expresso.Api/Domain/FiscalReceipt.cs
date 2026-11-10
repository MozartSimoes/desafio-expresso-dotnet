namespace Expresso.Api.Domain;

public enum FiscalReceiptStatus
{
    Pending,
    Issued,
    Failed
}

/// <summary>
/// "Nota fiscal": every delivered order needs a receipt, but the emitter is slow and sometimes down.
/// The receipt is saved as Pending together with the delivery and issued later in the background,
/// so the counter never waits for the emitter. Failures are retried with growing intervals
/// (exponential backoff) until the attempt limit, when it stays Failed for the manager to resend.
/// </summary>
public class FiscalReceipt
{
    private const int MaxErrorLength = 200;
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMinutes(10);

    public int Id { get; private set; }
    public int OrderId { get; private set; }
    public FiscalReceiptStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>When the background worker may try again; it ignores the receipt until then.</summary>
    public DateTimeOffset NextAttemptAt { get; private set; }

    public string? Number { get; private set; }
    public DateTimeOffset? IssuedAt { get; private set; }
    public string? LastError { get; private set; }

    private FiscalReceipt() { } // EF Core

    public static FiscalReceipt For(Order order, DateTimeOffset now)
    {
        if (order.Status != OrderStatus.Delivered)
            throw new InvalidOperationException("The receipt is issued only for delivered orders.");

        return new FiscalReceipt
        {
            OrderId = order.Id,
            Status = FiscalReceiptStatus.Pending,
            CreatedAt = now,
            NextAttemptAt = now
        };
    }

    public void MarkIssued(string number, DateTimeOffset now)
    {
        Attempts++;
        Status = FiscalReceiptStatus.Issued;
        Number = number;
        IssuedAt = now;
        LastError = null;
    }

    public void RegisterFailure(string error, DateTimeOffset now, int maxAttempts, TimeSpan baseDelay)
    {
        Attempts++;
        LastError = error.Length > MaxErrorLength ? error[..MaxErrorLength] : error;

        if (Attempts >= maxAttempts)
        {
            Status = FiscalReceiptStatus.Failed;
            return;
        }

        // 1x, 2x, 4x, 8x the base delay...: a struggling emitter gets room to recover instead of more load.
        var delay = baseDelay * Math.Pow(2, Attempts - 1);
        NextAttemptAt = now + (delay < MaxRetryDelay ? delay : MaxRetryDelay);
    }

    /// <summary>The manager resends a receipt that ran out of attempts (e.g. after the emitter is back).</summary>
    public void Retry(DateTimeOffset now)
    {
        if (Status != FiscalReceiptStatus.Failed)
            throw new ConflictException(
                $"Só é possível reenviar um cupom que falhou (situação atual: {Status.ToLabel()}).");

        Status = FiscalReceiptStatus.Pending;
        Attempts = 0;
        NextAttemptAt = now;
    }
}

public static class FiscalReceiptStatusExtensions
{
    public static string ToLabel(this FiscalReceiptStatus status) => status switch
    {
        FiscalReceiptStatus.Pending => "Pendente",
        FiscalReceiptStatus.Issued => "Emitido",
        FiscalReceiptStatus.Failed => "Falhou",
        _ => status.ToString()
    };
}
