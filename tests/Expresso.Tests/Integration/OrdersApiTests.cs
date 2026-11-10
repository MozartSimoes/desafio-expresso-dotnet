using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Expresso.Api.Data;
using Expresso.Api.Domain;
using Expresso.Api.Features.Orders;
using Expresso.Api.Features.Products;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Expresso.Tests.Integration;

[Collection(ApiCollection.Name)]
public class OrdersApiTests(ApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Creating_an_order_returns_201_with_total_calculated_by_the_api()
    {
        var latte = await CreateProductAsync(price: 12.00m);

        var response = await _client.PostAsJsonAsync("/api/orders",
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

        var update = await _client.PutAsJsonAsync($"/api/products/{latte.Id}",
            new ProductRequest(latte.Name, latte.Category, latte.Size, 20.00m), Json);
        update.EnsureSuccessStatusCode();

        var reloaded = await _client.GetFromJsonAsync<OrderResponse>($"/api/orders/{order.Id}", Json);
        Assert.Equal(12.00m, reloaded!.Items.Single().UnitPrice);
        Assert.Equal(12.00m, reloaded.Total);
    }

    [Fact]
    public async Task Ordering_an_unavailable_product_returns_422()
    {
        var product = await CreateProductAsync(available: false);

        var response = await _client.PostAsJsonAsync("/api/orders",
            new OrderRequest("Ana", [new OrderItemRequest(product.Id, 1, null)]), Json);

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "indisponível");
    }

    [Fact]
    public async Task Cancelling_after_preparation_started_returns_409()
    {
        var order = await PlaceOrderAsync((await CreateProductAsync()).Id);
        (await ChangeStatusAsync(order.Id, OrderStatus.Preparing)).EnsureSuccessStatusCode();

        var response = await ChangeStatusAsync(order.Id, OrderStatus.Canceled);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "Recebido");
    }

    [Fact]
    public async Task Unknown_status_returns_400_without_internal_details()
    {
        var order = await PlaceOrderAsync((await CreateProductAsync()).Id);

        var response = await _client.PatchAsync($"/api/orders/{order.Id}/status",
            JsonContent.Create(new { status = "Banana" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("Expresso.Api", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Concurrent_changes_to_the_same_order_are_detected()
    {
        var order = await PlaceOrderAsync((await CreateProductAsync()).Id);

        // Two baristas load the same order, both act on it, and only the first save may win.
        await using var scopeA = factory.Services.CreateAsyncScope();
        await using var scopeB = factory.Services.CreateAsyncScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<ExpressoDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<ExpressoDbContext>();
        var orderA = await dbA.Orders.SingleAsync(o => o.Id == order.Id);
        var orderB = await dbB.Orders.SingleAsync(o => o.Id == order.Id);

        orderA.ChangeStatus(OrderStatus.Preparing);
        await dbA.SaveChangesAsync();

        orderB.ChangeStatus(OrderStatus.Canceled);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync());
    }

    private async Task<ProductResponse> CreateProductAsync(decimal price = 10.00m, bool available = true)
    {
        // Unique name per call, so tests never collide on the (name, size) index.
        var request = new ProductRequest($"Teste {Guid.NewGuid():N}"[..20], ProductCategory.Coffee, ProductSize.M,
            price, available);
        var response = await _client.PostAsJsonAsync("/api/products", request, Json);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<ProductResponse>(response);
    }

    private async Task<OrderResponse> PlaceOrderAsync(int productId)
    {
        var response = await _client.PostAsJsonAsync("/api/orders",
            new OrderRequest("Ana", [new OrderItemRequest(productId, 1, null)]), Json);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<OrderResponse>(response);
    }

    private Task<HttpResponseMessage> ChangeStatusAsync(int orderId, OrderStatus status) =>
        _client.PatchAsJsonAsync($"/api/orders/{orderId}/status", new ChangeStatusRequest(status), Json);

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string detail)
    {
        Assert.Equal(status, response.StatusCode);
        var problem = await ReadAsync<ProblemDetails>(response);
        Assert.Contains(detail, problem.Detail);
    }
}
