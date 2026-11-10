using Expresso.Api.Domain;

namespace Expresso.Tests.Domain;

public class FiscalReceiptTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(5);
    private const int MaxAttempts = 3;

    private static Order DeliveredOrder()
    {
        var order = Order.Place("Ana", Now,
            [new OrderLine(new Product("Cappuccino", ProductCategory.Coffee, ProductSize.M, 11m), 1, null)], "Ana");
        foreach (var status in new[] { OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Delivered })
            order.ChangeStatus(status, Now, "Bruno");
        return order;
    }

    [Fact]
    public void New_receipt_is_pending_and_due_immediately()
    {
        var receipt = FiscalReceipt.For(DeliveredOrder(), Now);

        Assert.Equal(FiscalReceiptStatus.Pending, receipt.Status);
        Assert.Equal(Now, receipt.NextAttemptAt);
        Assert.Equal(0, receipt.Attempts);
    }

    [Fact]
    public void Receipt_cannot_be_created_before_delivery()
    {
        var order = Order.Place("Ana", Now,
            [new OrderLine(new Product("Cappuccino", ProductCategory.Coffee, ProductSize.M, 11m), 1, null)], "Ana");

        Assert.Throws<InvalidOperationException>(() => FiscalReceipt.For(order, Now));
    }

    [Fact]
    public void Each_failure_doubles_the_wait_before_the_next_attempt()
    {
        var receipt = FiscalReceipt.For(DeliveredOrder(), Now);

        receipt.RegisterFailure("fora do ar", Now, MaxAttempts, BaseDelay);
        Assert.Equal(Now + BaseDelay, receipt.NextAttemptAt);

        receipt.RegisterFailure("fora do ar", Now, MaxAttempts, BaseDelay);
        Assert.Equal(Now + 2 * BaseDelay, receipt.NextAttemptAt);

        Assert.Equal(FiscalReceiptStatus.Pending, receipt.Status);
        Assert.Equal("fora do ar", receipt.LastError);
    }

    [Fact]
    public void Wait_is_capped_at_ten_minutes()
    {
        var receipt = FiscalReceipt.For(DeliveredOrder(), Now);

        for (var i = 0; i < 20; i++)
            receipt.RegisterFailure("fora do ar", Now, maxAttempts: 100, BaseDelay);

        Assert.Equal(Now + TimeSpan.FromMinutes(10), receipt.NextAttemptAt);
    }

    [Fact]
    public void Receipt_fails_after_the_last_attempt_and_the_manager_can_resend_it()
    {
        var receipt = FiscalReceipt.For(DeliveredOrder(), Now);
        for (var i = 0; i < MaxAttempts; i++)
            receipt.RegisterFailure("fora do ar", Now, MaxAttempts, BaseDelay);
        Assert.Equal(FiscalReceiptStatus.Failed, receipt.Status);

        var later = Now.AddHours(1);
        receipt.Retry(later);

        Assert.Equal(FiscalReceiptStatus.Pending, receipt.Status);
        Assert.Equal(0, receipt.Attempts);
        Assert.Equal(later, receipt.NextAttemptAt);
    }

    [Fact]
    public void Issued_receipt_keeps_its_number_and_cannot_be_resent()
    {
        var receipt = FiscalReceipt.For(DeliveredOrder(), Now);
        receipt.RegisterFailure("fora do ar", Now, MaxAttempts, BaseDelay);

        receipt.MarkIssued("NFC-e 000000001", Now);

        Assert.Equal(FiscalReceiptStatus.Issued, receipt.Status);
        Assert.Equal("NFC-e 000000001", receipt.Number);
        Assert.Equal(2, receipt.Attempts);
        Assert.Null(receipt.LastError);
        var error = Assert.Throws<ConflictException>(() => receipt.Retry(Now));
        Assert.Contains("Emitido", error.Message);
    }

    [Fact]
    public void Long_error_messages_are_truncated_to_fit_the_column()
    {
        var receipt = FiscalReceipt.For(DeliveredOrder(), Now);

        receipt.RegisterFailure(new string('x', 500), Now, MaxAttempts, BaseDelay);

        Assert.Equal(200, receipt.LastError!.Length);
    }
}
