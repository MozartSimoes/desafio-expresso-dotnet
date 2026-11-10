using System.Net;
using System.Net.Http.Json;
using Expresso.Api.Domain;
using Expresso.Api.Features.Auth;
using Expresso.Api.Features.Orders;
using Expresso.Api.Features.Products;

namespace Expresso.Tests.Integration;

/// <summary>"Quem pode o quê": attendant takes orders, barista moves them along, manager runs the menu.</summary>
[Collection(ApiCollection.Name)]
public class AuthorizationApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Menu_is_public()
    {
        Assert.Equal(HttpStatusCode.OK, (await Anonymous.GetAsync("/api/products")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Anonymous.GetAsync("/api/menu/today")).StatusCode);
    }

    [Fact]
    public async Task Orders_require_login()
    {
        var response = await Anonymous.GetAsync("/api/orders");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_returns_401()
    {
        var response = await Anonymous.PostAsJsonAsync("/api/auth/login", new LoginRequest("gerente", "errada"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Only_the_manager_changes_the_menu()
    {
        var request = new ProductRequest("Mocha teste", ProductCategory.Coffee, ProductSize.G, 15m);

        Assert.Equal(HttpStatusCode.Forbidden, (await Attendant.PostAsJsonAsync("/api/products", request, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Barista.PostAsJsonAsync("/api/products", request, Json)).StatusCode);
    }

    [Fact]
    public async Task Barista_cannot_place_or_cancel_orders()
    {
        var product = await CreateProductAsync();
        var order = await PlaceOrderAsync(product.Id);

        var place = await Barista.PostAsJsonAsync("/api/orders",
            new OrderRequest("Ana", [new OrderItemRequest(product.Id, 1, null)]), Json);
        var cancel = await Barista.PostAsync($"/api/orders/{order.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.Forbidden, place.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, cancel.StatusCode);
    }

    [Fact]
    public async Task Attendant_cannot_move_the_order_along()
    {
        var order = await PlaceOrderAsync((await CreateProductAsync()).Id);

        var response = await AdvanceAsync(order.Id, OrderStatus.Preparing, Attendant);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Status_endpoint_points_to_the_cancel_endpoint()
    {
        var order = await PlaceOrderAsync((await CreateProductAsync()).Id);

        var response = await AdvanceAsync(order.Id, OrderStatus.Canceled);

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "/cancel");
    }
}
