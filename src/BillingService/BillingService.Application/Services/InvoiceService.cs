using BillingService.Application.Abstractions;
using BillingService.Application.Contracts;
using BillingService.Domain.Enums;

namespace BillingService.Application.Services;

public sealed class InvoiceService(IBillingStore store, IStockClient stock) : IInvoiceService
{
    public Task<PageResponse<InvoiceResponse>> ListAsync(int page, int pageSize, long? number, InvoiceStatus? status, CancellationToken cancellationToken)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue || number <= 0
            || (status.HasValue && !Enum.IsDefined(status.Value)))
            throw new BillingException(400, "VALIDATION_ERROR", "Informe página positiva, tamanho entre 1 e 100, número e status válidos.");
        return store.ListAsync(page, pageSize, number, status, cancellationToken);
    }

    public Task<InvoiceResponse> GetAsync(Guid id, CancellationToken cancellationToken) => store.GetAsync(id, cancellationToken);

    public async Task<InvoiceResponse> CreateAsync(CreateInvoiceRequest request, string correlationId, CancellationToken cancellationToken)
    {
        if (request.Items is null || request.Items.Count is < 1 or > 100
            || request.Items.Any(item => item is null || item.ProductId == Guid.Empty || item.Quantity <= 0))
            throw new BillingException(400, "VALIDATION_ERROR", "Informe de 1 a 100 itens, com produto válido e quantidade positiva.");

        IReadOnlyList<InvoiceItemRequest> items;
        try
        {
            items = request.Items.GroupBy(item => item.ProductId).OrderBy(group => group.Key)
                .Select(group => new InvoiceItemRequest(group.Key, checked((int)group.Sum(item => (long)item.Quantity)))).ToArray();
        }
        catch (OverflowException)
        {
            throw new BillingException(400, "VALIDATION_ERROR", "A soma das quantidades excedeu o limite permitido.");
        }

        ProductLookupResponse lookup;
        try { lookup = await stock.LookupAsync(items.Select(item => item.ProductId).ToArray(), correlationId, cancellationToken); }
        catch (StockTransportException)
        {
            throw new BillingException(503, "STOCK_UNAVAILABLE", "Não foi possível consultar os produtos. Tente novamente quando o estoque estiver disponível.");
        }
        if (lookup.MissingIds.Count > 0)
            throw new BillingException(400, "PRODUCT_NOT_FOUND", "Um ou mais produtos informados não existem.");

        return await store.CreateAsync(items, lookup.Products, cancellationToken);
    }

    public async Task<InvoiceResponse> PrintAsync(Guid invoiceId, string correlationId, CancellationToken cancellationToken)
    {
        var invoice = await store.GetAsync(invoiceId, cancellationToken);
        if (invoice.Status != InvoiceStatus.Open)
            throw new BillingException(409, "INVOICE_NOT_OPEN", "Somente notas abertas podem ser impressas.");

        var request = new StockDeductionRequest(invoice.Id,
            invoice.Items.Select(item => new InvoiceItemRequest(item.ProductId, item.Quantity)).ToArray());
        try
        {
            // O estoque reconhece a mesma nota e não repete uma baixa já concluída.
            await stock.DeductAsync(request, correlationId, cancellationToken);
        }
        catch (StockTransportException)
        {
            throw new BillingException(503, "STOCK_UNAVAILABLE",
                "Não foi possível confirmar a baixa. A nota permanece aberta; tente imprimir esta mesma nota novamente quando o estoque estiver disponível.");
        }

        // Só fecha após confirmar a baixa. Se este passo falhar, é seguro retentar a mesma nota.
        return await store.CloseAsync(invoice.Id, cancellationToken);
    }
}
