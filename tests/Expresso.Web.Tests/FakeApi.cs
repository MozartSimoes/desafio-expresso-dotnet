using System.Net;
using System.Net.Http.Json;
using Bunit;
using Expresso.Web.Api;
using Microsoft.Extensions.DependencyInjection;

namespace Expresso.Web.Tests;

/// <summary>Stands in for the Expresso API: each test says what each route answers and checks what was sent.</summary>
public class FakeApi : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> _routes = [];

    public List<(HttpMethod Method, string Path, string? Body, string? IdempotencyKey)> Requests { get; } = [];

    public FakeApi On(HttpMethod method, string pathAndQuery, object body, HttpStatusCode status = HttpStatusCode.OK) =>
        On(method, pathAndQuery, _ => new HttpResponseMessage(status) { Content = JsonContent.Create(body) });

    public FakeApi On(HttpMethod method, string pathAndQuery, Func<HttpRequestMessage, HttpResponseMessage> answer)
    {
        _routes[$"{method} {pathAndQuery}"] = answer;
        return this;
    }

    /// <summary>While set and not completed, every answer waits: lets a test look at the page during a slow API.</summary>
    public TaskCompletionSource? Hold { get; set; }

    public static HttpResponseMessage Problem(HttpStatusCode status, string detail) =>
        new(status) { Content = JsonContent.Create(new { title = "Erro", detail, status = (int)status }) };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.PathAndQuery;
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
        var key = request.Headers.TryGetValues("Idempotency-Key", out var values) ? values.Single() : null;
        Requests.Add((request.Method, path, body, key));
        if (Hold is not null)
            await Hold.Task;

        return _routes.TryGetValue($"{request.Method} {path}", out var answer)
            ? answer(request)
            : new HttpResponseMessage(HttpStatusCode.NotFound);
    }
}

/// <summary>A bUnit context with the fake API registered and, optionally, someone already logged in.</summary>
public abstract class WebTestBase : BunitContext
{
    protected FakeApi Api { get; } = new();
    protected Session Session { get; } = new();

    protected WebTestBase()
    {
        Services.AddSingleton(Session);
        Services.AddSingleton(new ExpressoApi(new HttpClient(Api) { BaseAddress = new Uri("http://api/") }, Session));
    }

    protected void LogInAs(string role, string name = "Teste") =>
        Session.SignIn(new LoginResponse("token-de-teste", name, role, DateTimeOffset.UtcNow.AddHours(8)));

    protected static OrderDto Order(int id, string status, string customer = "Maria") =>
        new(id, customer, DateTimeOffset.UtcNow, status, 11m, [new OrderItemDto(1, "Cappuccino", "M", 1, 11m, 11m, null)]);
}
