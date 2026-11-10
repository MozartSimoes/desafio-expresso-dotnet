namespace Expresso.Web.Api;

// The web app's own copy of the API contracts. It does not reference Expresso.Api, which would drag
// EF Core and the whole backend into the frontend; enums travel as text, exactly as in the JSON.

public record LoginRequest(string Username, string Password);
public record LoginResponse(string Token, string Name, string Role, DateTimeOffset ExpiresAt);

public record ProductDto(int Id, string Name, string Category, string? Size, decimal Price, bool Available, int? Stock);
public record ProductRequest(string Name, string Category, string? Size, decimal Price, bool Available, int? Stock);

public record OrderItemRequest(int ProductId, int Quantity, string? Notes);
public record OrderRequest(string CustomerName, IReadOnlyList<OrderItemRequest> Items);

public record OrderItemDto(
    int ProductId, string ProductName, string? Size, int Quantity, decimal UnitPrice, decimal Subtotal, string? Notes);

public record OrderDto(
    int Id, string CustomerName, DateTimeOffset CreatedAt, string Status, decimal Total, IReadOnlyList<OrderItemDto> Items);

public record OrderHistoryDto(string? From, string To, DateTimeOffset ChangedAt, string ChangedBy);

public record ReceiptDto(
    int OrderId, string Status, string? Number, int Attempts, DateTimeOffset CreatedAt,
    DateTimeOffset? IssuedAt, DateTimeOffset? NextAttemptAt, string? LastError);

public record WeatherDto(string City, double TemperatureC, string Condition);

public record MenuTodayDto(
    DateOnly Date, string OpeningHours, string? Holiday, WeatherDto? Weather, string Suggestion,
    IReadOnlyList<ProductDto> SuggestedProducts, IReadOnlyList<string> Warnings);

/// <summary>The RFC 7807 body the API sends on errors; validation errors come in <see cref="Errors"/>.</summary>
public record ProblemDto(string? Title, string? Detail, int? Status, Dictionary<string, string[]>? Errors);
