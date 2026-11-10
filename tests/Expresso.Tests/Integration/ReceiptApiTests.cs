using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Expresso.Api.Domain;
using Expresso.Api.Features.Receipts;

namespace Expresso.Tests.Integration;

/// <summary>"Nota fiscal": the emitter is slow and sometimes down, and the counter must not stop because of it.</summary>
[Collection(ApiCollection.Name)]
public class ReceiptApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    private FakeFiscalEmitter Emitter => Factory.FiscalEmitter;

    [Fact]
    public async Task Delivered_order_gets_its_receipt_in_the_background()
    {
        var order = await PlaceOrderAsync((await CreateProductAsync()).Id);

        await DeliverAsync(order.Id);

        var receipt = await WaitForReceiptAsync(order.Id, r => r.Status == FiscalReceiptStatus.Issued);
        Assert.StartsWith("TESTE", receipt.Number);
        Assert.Equal(1, receipt.Attempts);
    }

    [Fact]
    public async Task Emitter_failures_are_retried_until_it_works()
    {
        var order = await PlaceOrderAsync((await CreateProductAsync()).Id);
        Emitter.FailNext(order.Id, times: 2);

        await DeliverAsync(order.Id);

        var receipt = await WaitForReceiptAsync(order.Id, r => r.Status == FiscalReceiptStatus.Issued);
        Assert.Equal(3, receipt.Attempts);
        Assert.Null(receipt.LastError);
    }

    [Fact]
    public async Task Emitter_down_does_not_stop_the_counter_and_the_manager_can_resend_later()
    {
        var order = await PlaceOrderAsync((await CreateProductAsync()).Id);
        Emitter.FailNext(order.Id, times: int.MaxValue);

        await DeliverAsync(order.Id); // succeeds even though the emitter is down

        var failed = await WaitForReceiptAsync(order.Id, r => r.Status == FiscalReceiptStatus.Failed);
        Assert.Equal(3, failed.Attempts);
        Assert.Contains("fora do ar", failed.LastError);

        // Only the manager resends; then the emitter is back.
        Assert.Equal(HttpStatusCode.Forbidden, (await Barista.PostAsync($"/api/orders/{order.Id}/receipt/retry", null)).StatusCode);
        Emitter.FailNext(order.Id, times: 0);
        (await Manager.PostAsync($"/api/orders/{order.Id}/receipt/retry", null)).EnsureSuccessStatusCode();

        var issued = await WaitForReceiptAsync(order.Id, r => r.Status == FiscalReceiptStatus.Issued);
        Assert.Equal(1, issued.Attempts);
    }

    [Fact]
    public async Task Hanging_emitter_times_out_and_the_delivery_answers_at_once()
    {
        var order = await PlaceOrderAsync((await CreateProductAsync()).Id);
        Emitter.Hang(order.Id);

        var watch = Stopwatch.StartNew();
        await DeliverAsync(order.Id);
        watch.Stop();

        // The emitter never answers, yet the barista was not kept waiting: the delivery does not call it.
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(3), $"Delivery took {watch.Elapsed}");
        var failed = await WaitForReceiptAsync(order.Id, r => r.Status == FiscalReceiptStatus.Failed);
        Assert.Contains("não respondeu", failed.LastError);
        Emitter.Hang(order.Id, false);
    }

    [Fact]
    public async Task Receipt_exists_only_after_delivery()
    {
        var order = await PlaceOrderAsync((await CreateProductAsync()).Id);

        await AssertProblemAsync(await Attendant.GetAsync($"/api/orders/{order.Id}/receipt"),
            HttpStatusCode.Conflict, "entregue");
        await AssertProblemAsync(await Attendant.GetAsync("/api/orders/999999/receipt"),
            HttpStatusCode.NotFound, "Pedido 999999");
    }

    [Fact]
    public async Task Issued_receipt_cannot_be_resent()
    {
        var order = await PlaceOrderAsync((await CreateProductAsync()).Id);
        await DeliverAsync(order.Id);
        await WaitForReceiptAsync(order.Id, r => r.Status == FiscalReceiptStatus.Issued);

        await AssertProblemAsync(await Manager.PostAsync($"/api/orders/{order.Id}/receipt/retry", null),
            HttpStatusCode.Conflict, "Emitido");
    }

    private async Task DeliverAsync(int orderId)
    {
        foreach (var status in new[] { OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Delivered })
            (await AdvanceAsync(orderId, status)).EnsureSuccessStatusCode();
    }

    /// <summary>The worker runs on its own schedule, so the test polls until the receipt reaches the expected state.</summary>
    private async Task<FiscalReceiptResponse> WaitForReceiptAsync(int orderId, Func<FiscalReceiptResponse, bool> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        FiscalReceiptResponse? receipt = null;
        while (DateTime.UtcNow < deadline)
        {
            receipt = await Attendant.GetFromJsonAsync<FiscalReceiptResponse>($"/api/orders/{orderId}/receipt", Json);
            if (done(receipt!))
                return receipt!;
            await Task.Delay(100);
        }
        throw new TimeoutException($"Receipt of order {orderId} did not reach the expected state; last: {receipt}");
    }
}
