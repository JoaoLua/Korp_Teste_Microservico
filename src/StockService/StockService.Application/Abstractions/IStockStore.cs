using StockService.Application.Contracts;

namespace StockService.Application.Abstractions;

// Operations represent complete persistence units, including their transactions.
public interface IStockStore
{
    Task<PageResponse<ProductResponse>> ListProductsAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);
    Task<ProductResponse?> FindProductAsync(Guid id, CancellationToken cancellationToken);
    Task<ProductLookupResponse> LookupAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken);
    Task<ProductResponse> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken);
    Task<ProductResponse> UpdateProductAsync(Guid id, UpdateProductRequest request, byte[] expectedVersion, CancellationToken cancellationToken);
    Task<ProductResponse> AdjustStockAsync(Guid id, AdjustStockRequest request, byte[] expectedVersion, CancellationToken cancellationToken);
    Task<PageResponse<MovementResponse>> ListMovementsAsync(Guid productId, int page, int pageSize, CancellationToken cancellationToken);
    Task<DeductionResponse> DeductAsync(DeductStockRequest request, string requestHash, CancellationToken cancellationToken);
    Task<DeductionResponse?> FindDeductionAsync(Guid invoiceId, CancellationToken cancellationToken);
}
