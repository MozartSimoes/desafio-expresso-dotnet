using Expresso.Api.Domain;
using Microsoft.Extensions.Options;

namespace Expresso.Api.Features.Receipts;

/// <summary>The fiscal receipt emitter (in real life, an NFC-e provider or SEFAZ). Returns the receipt number.</summary>
public interface IFiscalEmitter
{
    /// <param name="receiptId">Sent as the reference, so a real emitter can recognise a retry of the same receipt.</param>
    Task<string> EmitAsync(int receiptId, Order order, CancellationToken ct);
}

public class FiscalEmitterException(string message) : Exception(message);

/// <summary>How the simulated emitter misbehaves; change it in appsettings or environment variables.</summary>
public class FiscalEmitterOptions
{
    public const string Section = "FiscalEmitter";

    /// <summary>How long each emission takes (the "slow" part).</summary>
    public int DelayMilliseconds { get; set; } = 1500;

    /// <summary>Chance of each emission failing, from 0 (never) to 1 (always down).</summary>
    public double FailureRate { get; set; } = 0.3;
}

public class SimulatedFiscalEmitter(IOptionsMonitor<FiscalEmitterOptions> options) : IFiscalEmitter
{
    public async Task<string> EmitAsync(int receiptId, Order order, CancellationToken ct)
    {
        // IOptionsMonitor: a changed appsettings.json takes effect without restarting the API.
        var current = options.CurrentValue;
        await Task.Delay(current.DelayMilliseconds, ct);

        if (Random.Shared.NextDouble() < current.FailureRate)
            throw new FiscalEmitterException("Emissor fiscal fora do ar (falha simulada).");

        // Derived from the receipt, not random: emitting the same receipt twice gives the same number.
        return $"NFC-e {receiptId:D9}";
    }
}
