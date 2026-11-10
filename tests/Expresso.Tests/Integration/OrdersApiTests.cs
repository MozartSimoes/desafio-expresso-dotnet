using System.Net;
using System.Net.Http.Json;
using Expresso.Api.Data;
using Expresso.Api.Domain;
using Expresso.Api.Features.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Expresso.Tests.Integration;

[Collection(ApiCollection.Name)]
public class OrdersApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Creating_an_order_returns_201_with_total_calculated_by_the_api()
    {
        var latte = await CreateProductAsync(price: 12.00m);

        var response = await Attendant.PostAsJsonAsync("/api/orders",
            new OrderRequest("Ana", [new OrderItemRequest(latte.Id, 2, "leite sem lactose")]), Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await ReadAsync<OrderResponse>(response);
        Assert.Equal(24.00m, order.Total);
        Assert.Equal(OrderStatus.Received, order.Status);
        Assert.Equal($"/api/orders/{order.Id}", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Changing_the_price_does_not_change_an_order_already_placed()
    {
        var latte = await CreateProductAsync(price: 12.00m);
        var order = await PlaceOrderAsync(latte.Id);

        var update = await Manager.PutAsJsonAsync($"/api/products/{latte.Id}",
            new Expresso.Api.Features.Products.ProductRequest(latte.Name, latte.Category, latte.Size, 20.00m), Json);
        update.EnsureSuccessStatusCode();

        var reloaded = await Attendant.GetFromJsonAsync<OrderResponse>($"/api/orders/{order.Id}", Json);
        Assert.Equal(12.00m, reloaded!.Items.Single().UnitPrice);
        Assert.Equal(12.00m, reloaded.Total);
    }

    [Fact]
    public async Task Ordering_an_unavailable_product_returns_422()
    {
        var product = await CreateProductAsync(available: false);

        var response = await Attendant.PostAsJsonAsync("/api/orders",
            new OrderRequest("Ana", [new OrderItemRequest(product.Id, 1, null)]), Json);

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "indisponível");
    }

    [Fact]
    public async Task Order_goes_from_received_to_delivered()
    {
        var order = await PlaceOrderAsync((await CreateProductAsync()).Id);

        foreach (var status in new[] { OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Delivered })
            (await AdvanceAsync(order.Id, status)).EnsureSuccessStatusCode();

        var reloaded = await Attendant.GetFromJsonAsync<OrderResponse>($"/api/orders/{order.Id}", Json);
        Assert.Equal(OrderStatus.Delivered, reloaded!.Status);
    }

    [Fact]
    public async Task Cancelling_while_received_is_allowed()
    {
        var order = await PlaceOrderAsync((await CreateProductAsync()).Id);

        var response = await Attendant.PostAsync($"/api/orders/{order.Id}/cancel", null);

        Assert.Equal(OrderStatus.Canceled, (await ReadAsync<OrderResponse>(response)).Status);
    }

    [Fact]
    public async Task Cancelling_after_preparation_started_returns_409()
    {
        var order = await PlaceOrderAsync((await CreateProductAsync()).Id);
        (await AdvanceAsync(order.Id, OrderStatus.Preparing)).EnsureSuccessStatusCode();

        var response = await Attendant.PostAsync($"/api/orders/{order.Id}/cancel", null);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "Recebido");
    }

    [Fact]
    public async Task Unknown_status_returns_400_without_internal_details()
    {
        var order = await PlaceOrderAsync((await CreateProductAsync()).Id);

        var response = await Barista.PatchAsync($"/api/orders/{order.Id}/status",
            JsonContent.Create(new { status = "Banana" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("Expresso.Api", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Concurrent_changes_to_the_same_order_are_detected()
    {
        var order = await PlaceOrderAsync((await CreateProductAsync()).Id);

        // Two baristas load the same order, both act on it, and only the first save may win.
        await using var scopeA = Factory.Services.CreateAsyncScope();
        await using var scopeB = Factory.Services.CreateAsyncScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<ExpressoDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<ExpressoDbContext>();
        var orderA = await dbA.Orders.SingleAsync(o => o.Id == order.Id);
        var orderB = await dbB.Orders.SingleAsync(o => o.Id == order.Id);

        orderA.ChangeStatus(OrderStatus.Preparing);
        await dbA.SaveChangesAsync();

        orderB.ChangeStatus(OrderStatus.Canceled);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync());
    }
}
