using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Expresso.Web.Api;

/// <summary>An error the API answered with; the message is the one the API wrote for people (in Portuguese).</summary>
public class ApiException(string message, HttpStatusCode status) : Exception(message)
{
    public HttpStatusCode Status { get; } = status;
}

/// <summary>Typed client for the Expresso API. Adds the logged-in user's token to every call.</summary>
public class ExpressoApi(HttpClient http, Session session)
{
    public async Task<LoginResponse> LoginAsync(string username, string password)
    {
        var response = await http.PostAsJsonAsync("api/auth/login", new LoginRequest(username, password));
        return await ReadAsync<LoginResponse>(response);
    }

    public Task<MenuTodayDto> GetMenuTodayAsync() => SendAsync<MenuTodayDto>(HttpMethod.Get, "api/menu/today");
    public Task<List<ProductDto>> GetProductsAsync() => SendAsync<List<ProductDto>>(HttpMethod.Get, "api/products");

    public Task<ProductDto> UpdateProductAsync(int id, ProductRequest request) =>
        SendAsync<ProductDto>(HttpMethod.Put, $"api/products/{id}", request);

    public Task<List<OrderDto>> GetOrdersAsync(string? customer = null) =>
        SendAsync<List<OrderDto>>(HttpMethod.Get,
            string.IsNullOrWhiteSpace(customer) ? "api/orders" : $"api/orders?customer={Uri.EscapeDataString(customer)}");

    public Task<OrderDto> GetOrderAsync(int id) => SendAsync<OrderDto>(HttpMethod.Get, $"api/orders/{id}");

    public Task<List<OrderHistoryDto>> GetHistoryAsync(int id) =>
        SendAsync<List<OrderHistoryDto>>(HttpMethod.Get, $"api/orders/{id}/history");

    /// <param name="idempotencyKey">Same key on a repeated click returns the same order instead of a new one.</param>
    public Task<OrderDto> PlaceOrderAsync(OrderRequest request, string idempotencyKey) =>
        SendAsync<OrderDto>(HttpMethod.Post, "api/orders", request,
            m => m.Headers.Add("Idempotency-Key", idempotencyKey));

    public Task<OrderDto> AdvanceAsync(int id, string status) =>
        SendAsync<OrderDto>(HttpMethod.Patch, $"api/orders/{id}/status", new { status });

    public Task<OrderDto> CancelAsync(int id) => SendAsync<OrderDto>(HttpMethod.Post, $"api/orders/{id}/cancel");

    /// <summary>Null while the order is not delivered yet (the API answers 409: no receipt before delivery).</summary>
    public async Task<ReceiptDto?> GetReceiptAsync(int orderId)
    {
        try
        {
            return await SendAsync<ReceiptDto>(HttpMethod.Get, $"api/orders/{orderId}/receipt");
        }
        catch (ApiException ex) when (ex.Status == HttpStatusCode.Conflict)
        {
            return null;
        }
    }

    public Task<ReceiptDto> RetryReceiptAsync(int orderId) =>
        SendAsync<ReceiptDto>(HttpMethod.Post, $"api/orders/{orderId}/receipt/retry");

    private async Task<T> SendAsync<T>(HttpMethod method, string url, object? body = null,
        Action<HttpRequestMessage>? configure = null)
    {
        using var message = new HttpRequestMessage(method, url);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        if (session.Token is not null)
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
        configure?.Invoke(message);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(message);
        }
        catch (HttpRequestException)
        {
            throw new ApiException("Não foi possível falar com a API. Ela está rodando?", HttpStatusCode.ServiceUnavailable);
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized && session.IsLoggedIn)
            session.SignOut(); // token expired: back to the login screen

        return await ReadAsync<T>(response);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        using (response)
        {
            if (response.IsSuccessStatusCode)
                return (await response.Content.ReadFromJsonAsync<T>())!;

            throw new ApiException(await ErrorMessageAsync(response), response.StatusCode);
        }
    }

    private static async Task<string> ErrorMessageAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Forbidden)
            return "Seu perfil não tem permissão para esta ação.";

        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
            if (problem?.Errors is { Count: > 0 } errors)
                return string.Join(" ", errors.Values.SelectMany(v => v));
            if (!string.IsNullOrWhiteSpace(problem?.Detail))
                return problem.Detail;
            if (!string.IsNullOrWhiteSpace(problem?.Title))
                return problem.Title;
        }
        catch (Exception)
        {
            // Not a ProblemDetails body; fall through to the generic message.
        }
        return $"A API respondeu com erro {(int)response.StatusCode}.";
    }
}
