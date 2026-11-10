using Expresso.Api.Domain;
using Expresso.Api.Features.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Expresso.Api.Features.Orders;

/// <summary>Customer orders, from the counter to delivery.</summary>
[ApiController]
[Route("api/orders")]
[Authorize(Roles = Roles.Staff)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class OrdersController(OrderService service) : ControllerBase
{
    // Name of the logged-in staff member, recorded in the order history.
    private string CurrentUser => User.Identity?.Name ?? "desconhecido";

    /// <summary>Lists the latest 100 orders, optionally filtered by status or part of the customer's name.</summary>
    [HttpGet]
    public Task<IReadOnlyList<OrderResponse>> List(
        [FromQuery] OrderStatus? status, [FromQuery] string? customer, CancellationToken ct) =>
        service.ListAsync(status, customer, ct);

    [HttpGet("{id:int}")]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<OrderResponse> Get(int id, CancellationToken ct) => service.GetAsync(id, ct);

    /// <summary>Everything that happened to the order: each status change, when and by whom.</summary>
    [HttpGet("{id:int}/history")]
    [ProducesResponseType<IReadOnlyList<OrderHistoryEntry>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IReadOnlyList<OrderHistoryEntry>> History(int id, CancellationToken ct) =>
        service.GetHistoryAsync(id, ct);

    /// <summary>Places an order (attendant). Prices and total are calculated by the API from the current menu.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.TakeOrders)]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrderResponse>> Create(OrderRequest request, CancellationToken ct)
    {
        var order = await service.CreateAsync(request, CurrentUser, ct);
        return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
    }

    /// <summary>Moves the order along Received → Preparing → Ready → Delivered (barista).</summary>
    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = Roles.PrepareOrders)]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public Task<OrderResponse> ChangeStatus(int id, ChangeStatusRequest request, CancellationToken ct) =>
        service.AdvanceStatusAsync(id, request.Status!.Value, CurrentUser, ct);

    /// <summary>Cancels the order (attendant). Only allowed while it is still Received.</summary>
    [HttpPost("{id:int}/cancel")]
    [Authorize(Roles = Roles.TakeOrders)]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<OrderResponse> Cancel(int id, CancellationToken ct) => service.CancelAsync(id, CurrentUser, ct);
}
