using BillingService.Application.Contracts;
using BillingService.Application.Services;
using BillingService.Domain.Enums;
using Xunit;

namespace BillingService.UnitTests;

public sealed class InvoiceServiceTests
{
    private const string Correlation = "unit-test";
    private readonly List<string> calls = [];
    private readonly FakeBillingStore store;
    private readonly FakeStockClient stock;
    private readonly InvoiceService service;

    public InvoiceServiceTests()
    {
        store = new(calls);
        stock = new(calls);
        service = new(store, stock);
    }

    public static IEnumerable<object[]> NotasInvalidas()
    {
        yield return [new CreateInvoiceRequest([])];
        yield return [new CreateInvoiceRequest(null)];
        yield return [new CreateInvoiceRequest([new(Guid.Empty, 1)])];
        yield return [new CreateInvoiceRequest([new(Guid.NewGuid(), 0)])];
        yield return [new CreateInvoiceRequest([new(Guid.NewGuid(), -1)])];
    }

    [Theory]
    [MemberData(nameof(NotasInvalidas))]
    public async Task Criar_rejeita_nota_sem_itens_validos(CreateInvoiceRequest request)
    {
        var error = await Assert.ThrowsAsync<BillingException>(() => service.CreateAsync(request, Correlation, default));

        Assert.Equal("VALIDATION_ERROR", error.Code);
        Assert.Equal(400, error.StatusCode);
        Assert.Empty(calls);
    }

    [Fact]
    public async Task Criar_rejeita_soma_de_quantidades_acima_do_limite()
    {
        var productId = Guid.NewGuid();
        var request = new CreateInvoiceRequest([new(productId, int.MaxValue), new(productId, 1)]);

        var error = await Assert.ThrowsAsync<BillingException>(() => service.CreateAsync(request, Correlation, default));

        Assert.Equal("VALIDATION_ERROR", error.Code);
        Assert.Empty(calls);
    }

    [Fact]
    public async Task Criar_consolida_produtos_repetidos_e_encaminha_dados_para_snapshot()
    {
        var product = new StockProduct(Guid.NewGuid(), "P001", "Descrição no momento da venda");
        stock.LookupResult = new([product], []);

        await service.CreateAsync(new([new(product.Id, 2), new(product.Id, 3)]), Correlation, default);

        Assert.Equal(new InvoiceItemRequest(product.Id, 5), Assert.Single(store.CreatedItems!));
        Assert.Equal(product, Assert.Single(store.SnapshotProducts!));
        Assert.Equal(product.Id, Assert.Single(stock.LookupIds!));
        Assert.Equal(new[] { "Lookup", "Create" }, calls);
    }

    [Fact]
    public async Task Criar_nao_salva_se_um_produto_nao_existe()
    {
        var productId = Guid.NewGuid();
        stock.LookupResult = new([], [productId]);

        var error = await Assert.ThrowsAsync<BillingException>(() =>
            service.CreateAsync(new([new(productId, 1)]), Correlation, default));

        Assert.Equal("PRODUCT_NOT_FOUND", error.Code);
        Assert.Null(store.CreatedItems);
    }

    [Fact]
    public async Task Criar_nao_salva_se_estoque_esta_indisponivel()
    {
        stock.LookupError = new StockTransportException("HTTP_ERROR", "Conexão indisponível.");

        var error = await Assert.ThrowsAsync<BillingException>(() =>
            service.CreateAsync(new([new(Guid.NewGuid(), 1)]), Correlation, default));

        Assert.Equal(503, error.StatusCode);
        Assert.Equal("STOCK_UNAVAILABLE", error.Code);
        Assert.Null(store.CreatedItems);
    }

    [Fact]
    public async Task Imprimir_nota_fechada_nao_chama_estoque()
    {
        store.Invoice = store.Invoice with { Status = InvoiceStatus.Closed, ClosedAt = DateTime.UtcNow };

        var error = await Assert.ThrowsAsync<BillingException>(() => service.PrintAsync(store.Invoice.Id, Correlation, default));

        Assert.Equal(409, error.StatusCode);
        Assert.Equal("INVOICE_NOT_OPEN", error.Code);
        Assert.Empty(stock.Deductions);
        Assert.Equal(0, store.CloseCount);
    }

    [Fact]
    public async Task Imprimir_fecha_nota_somente_apos_confirmacao_da_baixa()
    {
        var invoice = store.Invoice;

        var result = await service.PrintAsync(invoice.Id, Correlation, default);

        var request = Assert.Single(stock.Deductions);
        Assert.Equal(invoice.Id, request.InvoiceId);
        Assert.Equal(invoice.Items[0].ProductId, Assert.Single(request.Items).ProductId);
        Assert.Equal(invoice.Items[0].Quantity, request.Items[0].Quantity);
        Assert.Equal(new[] { "Get", "Deduct", "Close" }, calls);
        Assert.Equal(InvoiceStatus.Closed, result.Status);
        Assert.Equal(1, store.CloseCount);
    }

    [Fact]
    public async Task Imprimir_nao_fecha_nota_com_saldo_insuficiente()
    {
        stock.DeductionError = new BillingException(422, "INSUFFICIENT_STOCK", "Saldo insuficiente.");

        var error = await Assert.ThrowsAsync<BillingException>(() => service.PrintAsync(store.Invoice.Id, Correlation, default));

        Assert.Equal("INSUFFICIENT_STOCK", error.Code);
        Assert.Equal(InvoiceStatus.Open, store.Invoice.Status);
        Assert.Equal(0, store.CloseCount);
    }

    [Fact]
    public async Task Imprimir_apos_timeout_reutiliza_a_mesma_nota_e_so_fecha_no_sucesso()
    {
        var invoiceId = store.Invoice.Id;
        stock.DeductionError = new StockTransportException("TIMEOUT", "A resposta não chegou no prazo.");

        var error = await Assert.ThrowsAsync<BillingException>(() => service.PrintAsync(invoiceId, Correlation, default));

        Assert.Equal(503, error.StatusCode);
        Assert.Equal(InvoiceStatus.Open, store.Invoice.Status);
        Assert.Equal(0, store.CloseCount);

        stock.DeductionError = null;
        await service.PrintAsync(invoiceId, Correlation, default);

        Assert.Equal(2, stock.Deductions.Count);
        Assert.All(stock.Deductions, request => Assert.Equal(invoiceId, request.InvoiceId));
        Assert.Equal(stock.Deductions[0].Items, stock.Deductions[1].Items);
        Assert.Equal(1, store.CloseCount);
    }
}
