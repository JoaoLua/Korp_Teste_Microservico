using BillingService.Application.Abstractions;
using BillingService.Application.Contracts;
using BillingService.Domain.Enums;

namespace BillingService.UnitTests;

// Fakes registram o fluxo da Application, sem SQL, HTTP ou processos externos.
internal sealed class FakeBillingStore(List<string> calls) : IBillingStore
{
    public InvoiceResponse Invoice { get; set; } = new(Guid.NewGuid(), 1, InvoiceStatus.Open,
        DateTime.UtcNow, null, [new(Guid.NewGuid(), Guid.NewGuid(), "P001", "Produto", 2)]);
    public IReadOnlyList<InvoiceItemRequest>? CreatedItems { get; private set; }
    public IReadOnlyList<StockProduct>? SnapshotProducts { get; private set; }
    public int CloseCount { get; private set; }

    public Task<InvoiceResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        calls.Add("Get");
        return Task.FromResult(Invoice);
    }

    public Task<InvoiceResponse> CreateAsync(IReadOnlyList<InvoiceItemRequest> items,
        IReadOnlyList<StockProduct> products, CancellationToken cancellationToken)
    {
        calls.Add("Create");
        CreatedItems = items;
        SnapshotProducts = products;
        return Task.FromResult(Invoice);
    }

    public Task<InvoiceResponse> CloseAsync(Guid invoiceId, CancellationToken cancellationToken)
    {
        calls.Add("Close");
        CloseCount++;
        Invoice = Invoice with { Status = InvoiceStatus.Closed, ClosedAt = DateTime.UtcNow };
        return Task.FromResult(Invoice);
    }

    public Task<PageResponse<InvoiceResponse>> ListAsync(int page, int pageSize, long? number,
        InvoiceStatus? status, CancellationToken cancellationToken) => throw new NotSupportedException();
}

internal sealed class FakeStockClient(List<string> calls) : IStockClient
{
    public ProductLookupResponse LookupResult { get; set; } = new([], []);
    public IReadOnlyList<Guid>? LookupIds { get; private set; }
    public Exception? LookupError { get; set; }
    public Exception? DeductionError { get; set; }
    public List<StockDeductionRequest> Deductions { get; } = [];

    public Task<ProductLookupResponse> LookupAsync(IReadOnlyList<Guid> ids, string correlationId,
        CancellationToken cancellationToken)
    {
        calls.Add("Lookup");
        LookupIds = ids;
        if (LookupError is not null) throw LookupError;
        return Task.FromResult(LookupResult);
    }

    public Task<StockDeductionResponse> DeductAsync(StockDeductionRequest request, string correlationId,
        CancellationToken cancellationToken)
    {
        calls.Add("Deduct");
        Deductions.Add(request);
        if (DeductionError is not null) throw DeductionError;
        return Task.FromResult(new StockDeductionResponse(request.InvoiceId, "Completed", DateTime.UtcNow, request.Items));
    }
}
