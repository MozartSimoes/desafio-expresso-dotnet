using System.Net;
using System.Net.Http.Json;
using Expresso.Api.Domain;
using Expresso.Api.Features.Orders;
using Expresso.Api.Features.Products;

namespace Expresso.Tests.Integration;

/// <summary>"Pico das 8h": limited batches of snacks, sold by several attendants at the same time.</summary>
[Collection(ApiCollection.Name)]
public class StockApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Selling_decreases_stock_and_cancelling_returns_it()
    {
        var paoDeQueijo = await CreateSnackAsync(stock: 10);

        var order = await PlaceAsync(paoDeQueijo.Id, quantity: 3);
        Assert.Equal(7, await StockOfAsync(paoDeQueijo.Id));

        (await Attendant.PostAsync($"/api/orders/{order.Id}/cancel", null)).EnsureSuccessStatusCode();
        Assert.Equal(10, await StockOfAsync(paoDeQueijo.Id));
    }

    [Fact]
    public async Task Ordering_more_than_the_stock_returns_409_and_saves_nothing()
    {
        var coxinha = await CreateSnackAsync(stock: 2);
        var latte = await CreateProductAsync();

        var response = await Attendant.PostAsJsonAsync("/api/orders", new OrderRequest("Ana",
            [new OrderItemRequest(latte.Id, 1, null), new OrderItemRequest(coxinha.Id, 3, null)]), Json);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "Estoque insuficiente");
        Assert.Equal(2, await StockOfAsync(coxinha.Id));
    }

    [Fact]
    public async Task Two_lines_of_the_same_product_count_together()
    {
        var coxinha = await CreateSnackAsync(stock: 3);

        var response = await Attendant.PostAsJsonAsync("/api/orders", new OrderRequest("Ana",
            [new OrderItemRequest(coxinha.Id, 2, "bem quente"), new OrderItemRequest(coxinha.Id, 2, null)]), Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(3, await StockOfAsync(coxinha.Id));
    }

    [Fact]
    public async Task Last_unit_sold_by_many_attendants_at_once_is_sold_only_once()
    {
        var lastPaoDeQueijo = await CreateSnackAsync(stock: 1);

        // Ten attendants press "Finalizar" at the same moment for the last unit.
        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
            Attendant.PostAsJsonAsync("/api/orders",
                new OrderRequest("Cliente", [new OrderItemRequest(lastPaoDeQueijo.Id, 1, null)]), Json)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Equal(9, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(0, await StockOfAsync(lastPaoDeQueijo.Id));
    }

    private async Task<ProductResponse> CreateSnackAsync(int stock)
    {
        var request = new ProductRequest($"Salgado {Guid.NewGuid():N}"[..20], ProductCategory.Snack, null, 5m,
            Stock: stock);
        var response = await Manager.PostAsJsonAsync("/api/products", request, Json);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<ProductResponse>(response);
    }

    private async Task<OrderResponse> PlaceAsync(int productId, int quantity)
    {
        var response = await Attendant.PostAsJsonAsync("/api/orders",
            new OrderRequest("Ana", [new OrderItemRequest(productId, quantity, null)]), Json);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<OrderResponse>(response);
    }

    private async Task<int?> StockOfAsync(int productId) =>
        (await Anonymous.GetFromJsonAsync<ProductResponse>($"/api/products/{productId}", Json))!.Stock;
}
