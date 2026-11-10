using System.Net;
using Bunit;
using Expresso.Web.Api;
using Expresso.Web.Components.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Expresso.Web.Tests;

public class LoginTests : WebTestBase
{
    [Fact]
    public void Successful_login_keeps_the_session_and_opens_the_attendant_screen()
    {
        Api.On(HttpMethod.Post, "/api/auth/login",
            new LoginResponse("jwt", "Ana (atendente)", "Attendant", DateTimeOffset.UtcNow.AddHours(8)));
        var page = Render<Login>();

        page.Find("input").Change("atendente");
        page.Find("input[type=password]").Change("atendente123");
        page.Find("form").Submit();

        page.WaitForAssertion(() => Assert.True(Session.IsLoggedIn));
        Assert.Equal("Ana (atendente)", Session.Name);
        Assert.EndsWith("/novo-pedido", Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public void Wrong_password_shows_the_api_message()
    {
        Api.On(HttpMethod.Post, "/api/auth/login", _ => FakeApi.Problem(HttpStatusCode.Unauthorized, "Usuário ou senha inválidos."));
        var page = Render<Login>();

        page.Find("form").Submit();

        page.WaitForAssertion(() => Assert.Contains("Usuário ou senha inválidos.", page.Find(".alert.error").TextContent));
        Assert.False(Session.IsLoggedIn);
    }
}

public class ApiOfflineTests : WebTestBase
{
    // Found by hand: with the API stopped, the login crashed the page instead of showing a message.
    [Fact]
    public void Login_with_the_api_offline_shows_a_message_instead_of_crashing()
    {
        Api.On(HttpMethod.Post, "/api/auth/login", _ => throw new HttpRequestException("Connection refused"));
        var page = Render<Login>();

        page.Find("form").Submit();

        page.WaitForAssertion(() => Assert.Contains("Não foi possível falar com a API", page.Find(".alert.error").TextContent));
        Assert.False(Session.IsLoggedIn);
    }
}

public class BoardTests : WebTestBase
{
    private readonly List<OrderDto> _orders = [Order(1, "Received"), Order(2, "Preparing"), Order(3, "Ready")];

    public BoardTests() => Api.On(HttpMethod.Get, "/api/orders", _orders);

    [Fact]
    public void Visitor_without_login_is_sent_to_the_login_screen()
    {
        Render<Board>();

        Assert.Equal("http://localhost/", Services.GetRequiredService<NavigationManager>().Uri);
        Assert.Empty(Api.Requests);
    }

    [Fact]
    public void Orders_appear_in_the_column_of_their_status()
    {
        LogInAs("Barista");
        var page = Render<Board>();

        page.WaitForAssertion(() => Assert.Equal(3, page.FindAll(".order-card").Count));
        var columns = page.FindAll(".column");
        Assert.Contains("#1", columns[0].TextContent);
        Assert.Contains("#2", columns[1].TextContent);
        Assert.Contains("#3", columns[2].TextContent);
    }

    [Fact]
    public void Barista_moves_orders_forward_but_cannot_cancel()
    {
        LogInAs("Barista");
        Api.On(HttpMethod.Patch, "/api/orders/1/status", Order(1, "Preparing"));
        var page = Render<Board>();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll(".order-card")));

        Assert.DoesNotContain(page.FindAll("button"), b => b.TextContent.Contains("Cancelar"));
        page.FindAll("button").First(b => b.TextContent.Contains("Iniciar preparo")).Click();

        page.WaitForAssertion(() => Assert.Contains(Api.Requests, r => r.Method == HttpMethod.Patch));
        Assert.Contains("Preparing", Api.Requests.Single(r => r.Method == HttpMethod.Patch).Body);
    }

    [Fact]
    public void Attendant_can_cancel_a_received_order_but_not_prepare()
    {
        LogInAs("Attendant");
        var page = Render<Board>();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll(".order-card")));

        Assert.Single(page.FindAll("button"), b => b.TextContent.Contains("Cancelar"));
        Assert.DoesNotContain(page.FindAll("button"), b => b.TextContent.Contains("Iniciar preparo"));
    }

    [Fact]
    public void Api_error_is_shown_on_the_page()
    {
        LogInAs("Barista");
        Api.On(HttpMethod.Patch, "/api/orders/1/status",
            _ => FakeApi.Problem(HttpStatusCode.Conflict, "O registro foi alterado por outra pessoa."));
        var page = Render<Board>();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll(".order-card")));

        page.FindAll("button").First(b => b.TextContent.Contains("Iniciar preparo")).Click();

        page.WaitForAssertion(() => Assert.Contains("alterado por outra pessoa", page.Find(".alert.error").TextContent));
    }
}

public class NewOrderTests : WebTestBase
{
    private static readonly ProductDto Cappuccino = new(1, "Cappuccino", "Coffee", "M", 11m, true, null);
    private static readonly ProductDto PaoDeQueijo = new(9, "Pão de queijo", "Snack", null, 5m, true, 0);

    public NewOrderTests()
    {
        LogInAs("Attendant");
        Api.On(HttpMethod.Get, "/api/products", new[] { Cappuccino, PaoDeQueijo });
        Api.On(HttpMethod.Get, "/api/menu/today", _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
    }

    [Fact]
    public void Sold_out_products_cannot_be_added()
    {
        var page = Render<NewOrder>();

        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("button.product").Count));
        var paoDeQueijo = page.FindAll("button.product").Single(b => b.TextContent.Contains("Pão de queijo"));
        Assert.True(paoDeQueijo.HasAttribute("disabled"));
        Assert.Contains("esgotado", paoDeQueijo.TextContent);
    }

    [Fact]
    public void Retrying_after_a_failure_reuses_the_idempotency_key_and_a_new_order_gets_a_new_one()
    {
        var attempts = 0;
        Api.On(HttpMethod.Post, "/api/orders", _ => ++attempts == 1
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = System.Net.Http.Json.JsonContent.Create(Order(42, "Received"))
            });
        var page = Render<NewOrder>();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("button.product")));

        page.Find("aside input").Input("Maria");
        page.FindAll("button.product").First().Click();
        page.Find("button.big").Click();                      // fails
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll(".alert.error")));
        page.Find("button.big").Click();                      // retry succeeds
        page.WaitForAssertion(() => Assert.Contains("#42", page.Find(".alert.success").TextContent));

        var posts = Api.Requests.Where(r => r.Method == HttpMethod.Post).ToList();
        Assert.Equal(2, posts.Count);
        Assert.NotNull(posts[0].IdempotencyKey);
        Assert.Equal(posts[0].IdempotencyKey, posts[1].IdempotencyKey); // same order, same key

        page.FindAll("button.product").First().Click();
        page.Find("button.big").Click();                      // next customer
        page.WaitForAssertion(() => Assert.Equal(3, Api.Requests.Count(r => r.Method == HttpMethod.Post)));
        Assert.NotEqual(posts[0].IdempotencyKey, Api.Requests.Last(r => r.Method == HttpMethod.Post).IdempotencyKey);
    }

    [Fact]
    public void Barista_cannot_place_orders()
    {
        Session.SignOut();
        LogInAs("Barista");

        var page = Render<NewOrder>();

        Assert.Contains("Somente atendente e gerente", page.Markup);
        Assert.Empty(page.FindAll("button.product"));
    }
}

public class MenuTests : WebTestBase
{
    private static readonly ProductDto Cappuccino = new(1, "Cappuccino", "Coffee", "M", 11m, true, null);

    public MenuTests() => Api.On(HttpMethod.Get, "/api/products", new[] { Cappuccino });

    [Fact]
    public void Only_the_manager_can_edit_prices()
    {
        LogInAs("Attendant");

        var page = Render<Menu>();

        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("tbody tr")));
        Assert.All(page.FindAll("tbody input"), input => Assert.True(input.HasAttribute("disabled")));
    }

    [Fact]
    public void Manager_changes_the_price_and_saves()
    {
        LogInAs("Manager");
        Api.On(HttpMethod.Put, "/api/products/1", Cappuccino with { Price = 12.5m });
        var page = Render<Menu>();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("tbody tr")));

        page.Find("tbody input[type=number]").Change("12.5");
        page.Find("tbody button").Click();

        page.WaitForAssertion(() => Assert.Contains("atualizado", page.Find(".alert.success").TextContent));
        Assert.Contains("12.5", Api.Requests.Single(r => r.Method == HttpMethod.Put).Body);
    }
}
