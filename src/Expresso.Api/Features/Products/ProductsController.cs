using Expresso.Api.Domain;
using Expresso.Api.Features.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Expresso.Api.Features.Products;

/// <summary>Menu management.</summary>
[ApiController]
[Route("api/products")]
public class ProductsController(ProductService service) : ControllerBase
{
    /// <summary>Lists the menu, optionally filtered by category and availability.</summary>
    [HttpGet]
    [AllowAnonymous]
    public Task<IReadOnlyList<ProductResponse>> List(
        [FromQuery] ProductCategory? category, [FromQuery] bool? available, CancellationToken ct) =>
        service.ListAsync(category, available, ct);

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ProductResponse> Get(int id, CancellationToken ct) => service.GetAsync(id, ct);

    /// <summary>Adds a product to the menu (manager).</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Manager)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ProductResponse>> Create(ProductRequest request, CancellationToken ct)
    {
        var product = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
    }

    /// <summary>Updates a product (manager). Orders already placed keep the price they were sold at.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Manager)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public Task<ProductResponse> Update(int id, ProductRequest request, CancellationToken ct) =>
        service.UpdateAsync(id, request, ct);
}
