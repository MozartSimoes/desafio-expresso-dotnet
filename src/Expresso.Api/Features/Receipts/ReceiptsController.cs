using Expresso.Api.Features.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Expresso.Api.Features.Receipts;

/// <summary>The fiscal receipt of a delivered order, issued in the background.</summary>
[ApiController]
[Route("api/orders/{orderId:int}/receipt")]
[Authorize(Roles = Roles.Staff)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class ReceiptsController(ReceiptService service) : ControllerBase
{
    /// <summary>Receipt status: Pending (waiting for the emitter), Issued (with its number) or Failed.</summary>
    [HttpGet]
    [ProducesResponseType<FiscalReceiptResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<FiscalReceiptResponse> Get(int orderId, CancellationToken ct) => service.GetAsync(orderId, ct);

    /// <summary>Resends a receipt that ran out of attempts (manager), for example after the emitter is back.</summary>
    [HttpPost("retry")]
    [Authorize(Roles = Roles.Manager)]
    [ProducesResponseType<FiscalReceiptResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<FiscalReceiptResponse> Retry(int orderId, CancellationToken ct) =>
        service.RetryAsync(orderId, User.Identity?.Name ?? "desconhecido", ct);
}
