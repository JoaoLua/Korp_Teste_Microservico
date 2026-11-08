using Microsoft.AspNetCore.Mvc;
using StockService.Application.Contracts;
using StockService.Application.Services;

namespace StockService.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController(ProductService service) : ControllerBase
{
    [HttpGet]
    public Task<PageResponse<ProductResponse>> List([FromQuery] string? search = null, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        service.ListAsync(search, page, pageSize, cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<ProductResponse> Get(Guid id, CancellationToken cancellationToken) => service.GetAsync(id, cancellationToken);

    [HttpGet("lookup")]
    public Task<ProductLookupResponse> Lookup([FromQuery] Guid[] ids, CancellationToken cancellationToken) =>
        service.LookupAsync(ids, cancellationToken);

    [HttpPost]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ProductResponse>> Create(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var product = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
    }

    [HttpPost("improve-description")]
    public Task<ImprovedDescriptionResponse> ImproveDescription(ImproveDescriptionRequest request,
        [FromServices] DescriptionImprovementService descriptions, CancellationToken cancellationToken) =>
        descriptions.ImproveAsync(request, cancellationToken);

    [HttpPut("{id:guid}")]
    public Task<ProductResponse> Update(Guid id, UpdateProductRequest request, CancellationToken cancellationToken) =>
        service.UpdateAsync(id, request, cancellationToken);

    [HttpPost("{id:guid}/adjustments")]
    public Task<ProductResponse> Adjust(Guid id, AdjustStockRequest request, CancellationToken cancellationToken) =>
        service.AdjustAsync(id, request, cancellationToken);

    [HttpGet("{id:guid}/movements")]
    public Task<PageResponse<MovementResponse>> Movements(Guid id, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        service.MovementsAsync(id, page, pageSize, cancellationToken);
}
