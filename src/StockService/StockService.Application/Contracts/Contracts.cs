using System.Text.Json.Serialization;

namespace StockService.Application.Contracts;

public sealed record CreateProductRequest(string Code, string Description, [property: JsonRequired] int Balance, int MinimumStock = 0, string? Category = null);
public sealed record UpdateProductRequest(string Code, string Description, string RowVersion, int MinimumStock = 0, string? Category = null);
public sealed record AdjustStockRequest([property: JsonRequired] int Balance, string Reason, string RowVersion);
public sealed record ProductResponse(Guid Id, string Code, string Description, int Balance, int MinimumStock,
    string? Category, DateTime CreatedAt, DateTime? UpdatedAt, string RowVersion);
public sealed record ProductLookupResponse(IReadOnlyList<ProductResponse> Products, IReadOnlyList<Guid> MissingIds);
public sealed record PageResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
public sealed record MovementResponse(Guid Id, Guid ProductId, int Quantity, string MovementType, string? Reason,
    Guid? InvoiceId, DateTime CreatedAt);
public sealed record DeductionItem(Guid ProductId, int Quantity);
public sealed record DeductStockRequest(Guid InvoiceId, IReadOnlyList<DeductionItem> Items);
public sealed record DeductionResponse(Guid InvoiceId, string Status, DateTime CompletedAt,
    IReadOnlyList<DeductionItem> Items);
