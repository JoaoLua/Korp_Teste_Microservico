using BillingService.Application.Abstractions;
using BillingService.Application.Contracts;
using BillingService.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace BillingService.Controllers;

[ApiController]
[Route("api/invoices")]
public sealed class InvoicesController(IInvoiceService invoices) : ControllerBase
{
    [HttpGet]
    public Task<PageResponse<InvoiceResponse>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] long? number = null, [FromQuery] InvoiceStatus? status = null, CancellationToken cancellationToken = default)
        => invoices.ListAsync(page, pageSize, number, status, cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<InvoiceResponse> Get(Guid id, CancellationToken cancellationToken) => invoices.GetAsync(id, cancellationToken);

    [HttpPost]
    [ProducesResponseType<InvoiceResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<InvoiceResponse>> Create(CreateInvoiceRequest request, CancellationToken cancellationToken)
    {
        var invoice = await invoices.CreateAsync(request, CorrelationId, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = invoice.Id }, invoice);
    }

    [HttpPost("{id:guid}/print")]
    [ProducesResponseType<InvoiceResponse>(StatusCodes.Status200OK)]
    public Task<InvoiceResponse> Print(Guid id, CancellationToken cancellationToken)
        => invoices.PrintAsync(id, CorrelationId, cancellationToken);

    private string CorrelationId => HttpContext.Items["CorrelationId"] as string ?? HttpContext.TraceIdentifier;
}
