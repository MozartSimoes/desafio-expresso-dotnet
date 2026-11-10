extern alias web;
using web::Expresso.Web.Api;
using WebOrderItem = web::Expresso.Web.Api.OrderItemRequest;
using WebOrderRequest = web::Expresso.Web.Api.OrderRequest;

namespace Expresso.Tests.Integration;

/// <summary>
/// The web app keeps its own copy of the API contracts. This test runs the web app's client against the real API,
/// so a renamed field or a changed type breaks here instead of showing up as an empty screen.
/// </summary>
[Collection(ApiCollection.Name)]
public class WebClientContractTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Web_client_reads_every_api_response_it_uses()
    {
        var session = new Session();
        var api = new ExpressoApi(Factory.CreateClient(), session);

        session.SignIn(await api.LoginAsync("gerente", "gerente123"));
        Assert.Equal("Manager", session.Role);

        var products = await api.GetProductsAsync();
        var product = products.First(p => p.Available);
        Assert.False(string.IsNullOrEmpty(product.Category));

        var menu = await api.GetMenuTodayAsync();
        Assert.NotEmpty(menu.OpeningHours);

        var updated = await api.UpdateProductAsync(product.Id,
            new ProductRequest(product.Name, product.Category, product.Size, product.Price, true, product.Stock));
        Assert.Equal(product.Price, updated.Price);

        var key = Guid.NewGuid().ToString();
        var request = new WebOrderRequest("Contrato", [new WebOrderItem(product.Id, 2, "sem açúcar")]);
        var order = await api.PlaceOrderAsync(request, key);
        Assert.Equal("Received", order.Status);
        Assert.Equal(product.Price * 2, order.Total);
        Assert.Equal("sem açúcar", order.Items.Single().Notes);
        Assert.Equal(order.Id, (await api.PlaceOrderAsync(request, key)).Id); // toque duplo

        Assert.Null(await api.GetReceiptAsync(order.Id)); // no receipt before delivery
        foreach (var status in new[] { "Preparing", "Ready", "Delivered" })
            Assert.Equal(status, (await api.AdvanceAsync(order.Id, status)).Status);

        var history = await api.GetHistoryAsync(order.Id);
        Assert.Equal([null, "Received", "Preparing", "Ready"], history.Select(h => h.From));
        Assert.Equal("Gabi (gerente)", history.Last().ChangedBy);

        Assert.NotNull(await api.GetReceiptAsync(order.Id));
        Assert.Contains(await api.GetOrdersAsync("contrato"), o => o.Id == order.Id);
        Assert.Equal(order.Id, (await api.GetOrderAsync(order.Id)).Id);
    }

    [Fact]
    public async Task Web_client_shows_the_api_message_on_errors()
    {
        var session = new Session();
        var api = new ExpressoApi(Factory.CreateClient(), session);
        session.SignIn(await api.LoginAsync("barista", "barista123"));
        var order = await PlaceOrderAsync((await CreateProductAsync()).Id);

        var forbidden = await Assert.ThrowsAsync<ApiException>(() => api.CancelAsync(order.Id));
        Assert.Contains("permissão", forbidden.Message);

        var invalid = await Assert.ThrowsAsync<ApiException>(() => api.AdvanceAsync(order.Id, "Delivered"));
        Assert.Contains("Recebido para Entregue", invalid.Message);

        var wrongLogin = await Assert.ThrowsAsync<ApiException>(() => api.LoginAsync("barista", "errada"));
        Assert.Contains("inválidos", wrongLogin.Message);
    }
}
