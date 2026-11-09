using Expresso.Api.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Expresso.Api.Features.Orders;

/// <summary>Customer orders, from the counter to delivery.</summary>
[ApiController]
[Route("api/orders")]
public class OrdersController(OrderService service) : ControllerBase
{
    /// <summary>Lists the latest 100 orders, optionally filtered by status.</summary>
    [HttpGet]
    public Task<IReadOnlyList<OrderResponse>> List([FromQuery] OrderStatus? status, CancellationToken ct) =>
        service.ListAsync(status, ct);

    [HttpGet("{id:int}")]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<OrderResponse> Get(int id, CancellationToken ct) => service.GetAsync(id, ct);

    /// <summary>Places an order. Prices and total are calculated by the API from the current menu.</summary>
    [HttpPost]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrderResponse>> Create(OrderRequest request, CancellationToken ct)
    {
        var order = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
    }
}
