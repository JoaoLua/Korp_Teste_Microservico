using BillingService.Application.Contracts;
using BillingService.Domain.Enums;

namespace BillingService.Application.Abstractions;

public interface IStockClient
{
    Task<ProductLookupResponse> LookupAsync(IReadOnlyList<Guid> ids, string correlationId, CancellationToken cancellationToken);
    Task<StockDeductionResponse> DeductAsync(StockDeductionRequest request, string correlationId, CancellationToken cancellationToken);
}

public interface IBillingStore
{
    Task<PageResponse<InvoiceResponse>> ListAsync(int page, int pageSize, long? number, InvoiceStatus? status, CancellationToken cancellationToken);
    Task<InvoiceResponse> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<InvoiceResponse> CreateAsync(IReadOnlyList<InvoiceItemRequest> items, IReadOnlyList<StockProduct> products, CancellationToken cancellationToken);
    Task<InvoiceResponse> CloseAsync(Guid invoiceId, CancellationToken cancellationToken);
}

public interface IInvoiceService
{
    Task<PageResponse<InvoiceResponse>> ListAsync(int page, int pageSize, long? number, InvoiceStatus? status, CancellationToken cancellationToken);
    Task<InvoiceResponse> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<InvoiceResponse> CreateAsync(CreateInvoiceRequest request, string correlationId, CancellationToken cancellationToken);
    Task<InvoiceResponse> PrintAsync(Guid invoiceId, string correlationId, CancellationToken cancellationToken);
}
