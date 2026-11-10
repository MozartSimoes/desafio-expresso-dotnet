using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Expresso.Api.Domain;
using Expresso.Api.Features.Orders;
using Expresso.Api.Features.Products;
using Microsoft.AspNetCore.Mvc;

namespace Expresso.Tests.Integration;

/// <summary>Logged-in clients for each role and helpers that create the data a test needs.</summary>
public abstract class ApiTestBase(ApiFactory factory) : IAsyncLifetime
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    protected ApiFactory Factory { get; } = factory;
    protected HttpClient Anonymous { get; } = factory.CreateClient();
    protected HttpClient Attendant { get; private set; } = null!;
    protected HttpClient Barista { get; private set; } = null!;
    protected HttpClient Manager { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Attendant = await Factory.CreateClientAsAsync("atendente");
        Barista = await Factory.CreateClientAsAsync("barista");
        Manager = await Factory.CreateClientAsAsync("gerente");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    protected async Task<ProductResponse> CreateProductAsync(decimal price = 10.00m, bool available = true)
    {
        // Unique name per call, so tests never collide on the (name, size) index.
        var request = new ProductRequest($"Teste {Guid.NewGuid():N}"[..20], ProductCategory.Coffee, ProductSize.M,
            price, available);
        var response = await Manager.PostAsJsonAsync("/api/products", request, Json);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<ProductResponse>(response);
    }

    protected async Task<OrderResponse> PlaceOrderAsync(int productId)
    {
        var response = await Attendant.PostAsJsonAsync("/api/orders",
            new OrderRequest("Ana", [new OrderItemRequest(productId, 1, null)]), Json);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<OrderResponse>(response);
    }

    protected Task<HttpResponseMessage> AdvanceAsync(int orderId, OrderStatus status, HttpClient? client = null) =>
        (client ?? Barista).PatchAsJsonAsync($"/api/orders/{orderId}/status", new ChangeStatusRequest(status), Json);

    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;

    protected static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string detail)
    {
        Assert.Equal(status, response.StatusCode);
        var problem = await ReadAsync<ProblemDetails>(response);
        Assert.Contains(detail, problem.Detail);
    }
}
