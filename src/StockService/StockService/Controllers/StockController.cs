using Microsoft.AspNetCore.Mvc;
using StockService.Application.Contracts;
using StockService.Application.Services;

namespace StockService.Controllers;

[ApiController]
[Route("api/stock")]
public sealed class StockController(StockDeductionService service) : ControllerBase
{
    [HttpPost("deduct")]
    [ProducesResponseType<DeductionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DeductionResponse>> Deduct(DeductStockRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await service.DeductAsync(request, cancellationToken));
    }

    [HttpGet("deductions/{invoiceId:guid}")]
    public Task<DeductionResponse> Get(Guid invoiceId, CancellationToken cancellationToken) =>
        service.GetAsync(invoiceId, cancellationToken);
}
