using StockService.Application.Abstractions;
using StockService.Application.Contracts;

namespace StockService.UnitTests;

// Registra chamadas da Application; não simula transações nem um banco de dados.
internal sealed class FakeStockStore : IStockStore
{
    public CreateProductRequest? CreatedProduct { get; private set; }
    public AdjustStockRequest? Adjustment { get; private set; }
    public byte[]? ExpectedVersion { get; private set; }
    public List<(DeductStockRequest Request, string Hash)> Deductions { get; } = [];
    public Exception? DeductionError { get; set; }

    public Task<ProductResponse> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken)
    {
        CreatedProduct = request;
        return Task.FromResult(new ProductResponse(Guid.NewGuid(), request.Code, request.Description,
            request.Balance, request.MinimumStock, request.Category, DateTime.UtcNow, null,
            Convert.ToBase64String(new byte[8])));
    }

    public Task<ProductResponse?> FindProductAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<ProductResponse?>(null);

    public Task<ProductResponse> AdjustStockAsync(Guid id, AdjustStockRequest request, byte[] expectedVersion,
        CancellationToken cancellationToken)
    {
        Adjustment = request;
        ExpectedVersion = expectedVersion;
        return Task.FromResult(new ProductResponse(id, "P001", "Produto", request.Balance, 0, null,
            DateTime.UtcNow, DateTime.UtcNow, request.RowVersion));
    }

    public Task<DeductionResponse> DeductAsync(DeductStockRequest request, string requestHash,
        CancellationToken cancellationToken)
    {
        Deductions.Add((request, requestHash));
        if (DeductionError is not null) throw DeductionError;
        return Task.FromResult(new DeductionResponse(request.InvoiceId, "Completed", DateTime.UtcNow, request.Items));
    }

    public Task<PageResponse<ProductResponse>> ListProductsAsync(string? search, int page, int pageSize,
        CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<ProductLookupResponse> LookupAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
    public Task<ProductResponse> UpdateProductAsync(Guid id, UpdateProductRequest request, byte[] expectedVersion,
        CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<PageResponse<MovementResponse>> ListMovementsAsync(Guid productId, int page, int pageSize,
        CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<DeductionResponse?> FindDeductionAsync(Guid invoiceId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
