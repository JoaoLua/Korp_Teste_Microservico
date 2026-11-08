using BillingService.Domain.Enums;

namespace BillingService.Application.Contracts;

public sealed record InvoiceItemRequest(Guid ProductId, int Quantity);
public sealed record CreateInvoiceRequest(IReadOnlyList<InvoiceItemRequest>? Items);
public sealed record InvoiceItemResponse(Guid Id, Guid ProductId, string ProductCode, string ProductDescription, int Quantity);
public sealed record InvoiceResponse(Guid Id, long Number, InvoiceStatus Status, DateTime CreatedAt,
    DateTime? ClosedAt, IReadOnlyList<InvoiceItemResponse> Items);
public sealed record PageResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalCount);

public sealed record StockProduct(Guid Id, string Code, string Description);
public sealed record ProductLookupResponse(IReadOnlyList<StockProduct> Products, IReadOnlyList<Guid> MissingIds);
public sealed record StockDeductionRequest(Guid InvoiceId, IReadOnlyList<InvoiceItemRequest> Items);
public sealed record StockDeductionResponse(Guid InvoiceId, string Status, DateTime CompletedAt, IReadOnlyList<InvoiceItemRequest> Items);

public sealed class BillingException(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}

public sealed class StockTransportException(string code, string message)
    : Exception(message)
{
    public string Code { get; } = code;
}
