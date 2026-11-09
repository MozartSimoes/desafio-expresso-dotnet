using System.ComponentModel.DataAnnotations;
using Expresso.Api.Domain;

namespace Expresso.Api.Features.Orders;

public record OrderItemRequest(
    int ProductId,
    int Quantity,
    [StringLength(140, ErrorMessage = "A observação deve ter no máximo 140 caracteres.")] string? Notes);

public record OrderRequest(
    [Required(ErrorMessage = "Informe o nome do cliente.")]
    [StringLength(60, MinimumLength = 2, ErrorMessage = "O nome do cliente deve ter entre 2 e 60 caracteres.")]
    string CustomerName,
    [Required(ErrorMessage = "Informe os itens do pedido.")] IReadOnlyList<OrderItemRequest> Items);

public record OrderItemResponse(
    int ProductId, string ProductName, ProductSize? Size, int Quantity, decimal UnitPrice, decimal Subtotal, string? Notes);

public record OrderResponse(
    int Id, string CustomerName, DateTimeOffset CreatedAt, OrderStatus Status, decimal Total,
    IReadOnlyList<OrderItemResponse> Items)
{
    public static OrderResponse From(Order o) => new(
        o.Id, o.CustomerName, o.CreatedAt, o.Status, o.Total,
        o.Items.Select(i => new OrderItemResponse(
            i.ProductId, i.ProductName, i.Size, i.Quantity, i.UnitPrice, i.Subtotal, i.Notes)).ToList());
}
