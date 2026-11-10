using Expresso.Api.Domain;

namespace Expresso.Tests.Domain;

public class OrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);

    private static Product Cappuccino(decimal price = 11.00m, bool available = true) =>
        new("Cappuccino", ProductCategory.Coffee, ProductSize.M, price, available);

    private static Product PaoDeQueijo() => new("Pão de queijo", ProductCategory.Snack, null, 5.00m);

    private static Order PlaceSimpleOrder() =>
        Order.Place("Ana", Now, [new OrderLine(Cappuccino(), 1, null)]);

    [Fact]
    public void Place_starts_as_received_with_customer_and_time()
    {
        var order = PlaceSimpleOrder();

        Assert.Equal(OrderStatus.Received, order.Status);
        Assert.Equal("Ana", order.CustomerName);
        Assert.Equal(Now, order.CreatedAt);
    }

    // Rule 3: the total is calculated by the API.
    [Fact]
    public void Place_calculates_total_from_items()
    {
        var order = Order.Place("Ana", Now,
        [
            new OrderLine(Cappuccino(), 2, "sem açúcar"),
            new OrderLine(PaoDeQueijo(), 3, null)
        ]);

        Assert.Equal(2 * 11.00m + 3 * 5.00m, order.Total);
        Assert.Equal("sem açúcar", order.Items.First().Notes);
    }

    // Rule 4: changing a product's price does not change orders already placed.
    [Fact]
    public void Item_keeps_the_price_from_when_the_order_was_placed()
    {
        var cappuccino = Cappuccino(price: 11.00m);
        var order = Order.Place("Ana", Now, [new OrderLine(cappuccino, 1, null)]);

        cappuccino.Update("Cappuccino", ProductCategory.Coffee, ProductSize.M, 15.00m, available: true);

        Assert.Equal(11.00m, order.Items.Single().UnitPrice);
        Assert.Equal(11.00m, order.Total);
    }

    // Rule 1: an unavailable product cannot be ordered.
    [Fact]
    public void Place_rejects_unavailable_product()
    {
        var ex = Assert.Throws<BusinessRuleException>(() =>
            Order.Place("Ana", Now, [new OrderLine(Cappuccino(available: false), 1, null)]));

        Assert.Contains("indisponível", ex.Message);
    }

    // Rule 2: every order has at least one item...
    [Fact]
    public void Place_rejects_order_without_items()
    {
        Assert.Throws<BusinessRuleException>(() => Order.Place("Ana", Now, []));
    }

    // ...with a quantity greater than zero.
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Place_rejects_quantity_not_greater_than_zero(int quantity)
    {
        Assert.Throws<BusinessRuleException>(() =>
            Order.Place("Ana", Now, [new OrderLine(Cappuccino(), quantity, null)]));
    }

    // Rule 5: Received → Preparing → Ready → Delivered.
    [Fact]
    public void Status_follows_the_whole_flow_until_delivered()
    {
        var order = PlaceSimpleOrder();

        order.ChangeStatus(OrderStatus.Preparing);
        order.ChangeStatus(OrderStatus.Ready);
        order.ChangeStatus(OrderStatus.Delivered);

        Assert.Equal(OrderStatus.Delivered, order.Status);
    }

    [Theory]
    [InlineData(OrderStatus.Ready)]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Received)]
    public void Status_cannot_skip_or_repeat_steps(OrderStatus next)
    {
        var order = PlaceSimpleOrder();

        Assert.Throws<ConflictException>(() => order.ChangeStatus(next));
    }

    // Rule 5: cancellation is only allowed while the order is Received.
    [Fact]
    public void Cancel_is_allowed_while_received()
    {
        var order = PlaceSimpleOrder();

        order.ChangeStatus(OrderStatus.Canceled);

        Assert.Equal(OrderStatus.Canceled, order.Status);
    }

    [Theory]
    [InlineData(1)] // Preparing
    [InlineData(2)] // Ready
    [InlineData(3)] // Delivered
    public void Cancel_is_rejected_after_received(int stepsForward)
    {
        var order = PlaceSimpleOrder();
        OrderStatus[] flow = [OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Delivered];
        foreach (var status in flow.Take(stepsForward))
            order.ChangeStatus(status);

        var ex = Assert.Throws<ConflictException>(() => order.ChangeStatus(OrderStatus.Canceled));
        Assert.Contains("só pode ser cancelado enquanto estiver Recebido", ex.Message);
    }

    [Fact]
    public void Canceled_order_cannot_move_again()
    {
        var order = PlaceSimpleOrder();
        order.ChangeStatus(OrderStatus.Canceled);

        Assert.Throws<ConflictException>(() => order.ChangeStatus(OrderStatus.Preparing));
    }
}
