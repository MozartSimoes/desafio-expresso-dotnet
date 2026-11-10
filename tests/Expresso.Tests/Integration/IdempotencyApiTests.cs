using System.Net;
using System.Net.Http.Json;
using Expresso.Api.Domain;
using Expresso.Api.Features.Orders;
using Expresso.Api.Features.Products;

namespace Expresso.Tests.Integration;

/// <summary>"Toque duplo": the totem sends "Finalizar" twice; the customer must end up with a single order.</summary>
[Collection(ApiCollection.Name)]
public class IdempotencyApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Repeating_the_request_with_the_same_key_returns_the_same_order()
    {
        var (request, key) = (await NewOrderRequestAsync(), Guid.NewGuid().ToString());

        var first = await PostWithKeyAsync(request, key);
        var second = await PostWithKeyAsync(request, key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal((await ReadAsync<OrderResponse>(first)).Id, (await ReadAsync<OrderResponse>(second)).Id);
        Assert.Single(await OrdersOfAsync(request.CustomerName));
    }

    [Fact]
    public async Task Taps_arriving_at_the_same_instant_create_a_single_order_and_reserve_stock_once()
    {
        var snack = await CreateSnackAsync(stock: 10);
        var request = new OrderRequest($"Totem {Guid.NewGuid():N}"[..20], [new OrderItemRequest(snack.Id, 2, null)]);
        var key = Guid.NewGuid().ToString();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => PostWithKeyAsync(request, key)));

        Assert.All(responses, r => Assert.True(r.IsSuccessStatusCode, $"Unexpected {r.StatusCode}"));
        var ids = await Task.WhenAll(responses.Select(async r => (await ReadAsync<OrderResponse>(r)).Id));
        Assert.Single(ids.Distinct());
        Assert.Single(await OrdersOfAsync(request.CustomerName));
        Assert.Equal(8, (await Anonymous.GetFromJsonAsync<ProductResponse>($"/api/products/{snack.Id}", Json))!.Stock);
    }

    [Fact]
    public async Task Same_key_with_a_different_order_returns_409()
    {
        var key = Guid.NewGuid().ToString();
        (await PostWithKeyAsync(await NewOrderRequestAsync(), key)).EnsureSuccessStatusCode();

        var response = await PostWithKeyAsync(await NewOrderRequestAsync(), key);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "Idempotency-Key");
    }

    [Fact]
    public async Task Without_a_key_each_request_is_a_new_order()
    {
        var request = await NewOrderRequestAsync();

        (await Attendant.PostAsJsonAsync("/api/orders", request, Json)).EnsureSuccessStatusCode();
        (await Attendant.PostAsJsonAsync("/api/orders", request, Json)).EnsureSuccessStatusCode();

        Assert.Equal(2, (await OrdersOfAsync(request.CustomerName)).Count);
    }

    private async Task<OrderRequest> NewOrderRequestAsync() =>
        new($"Totem {Guid.NewGuid():N}"[..20], [new OrderItemRequest((await CreateProductAsync()).Id, 1, null)]);

    private Task<HttpResponseMessage> PostWithKeyAsync(OrderRequest request, string key)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/orders") { Content = JsonContent.Create(request, options: Json) };
        message.Headers.Add("Idempotency-Key", key);
        return Attendant.SendAsync(message);
    }

    private async Task<List<OrderResponse>> OrdersOfAsync(string customer) =>
        (await Attendant.GetFromJsonAsync<List<OrderResponse>>($"/api/orders?customer={customer}", Json))!;

    private async Task<ProductResponse> CreateSnackAsync(int stock)
    {
        var request = new ProductRequest($"Salgado {Guid.NewGuid():N}"[..20], ProductCategory.Snack, null, 5m, Stock: stock);
        var response = await Manager.PostAsJsonAsync("/api/products", request, Json);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<ProductResponse>(response);
    }
}
