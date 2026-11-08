using StockService.Application.Common;
using StockService.Application.Contracts;
using StockService.Application.Services;
using StockService.Domain.Entities;
using Xunit;

namespace StockService.UnitTests;

public sealed class StockDeductionServiceTests
{
    private static readonly Guid ProductA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ProductB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static IEnumerable<object[]> PedidosInvalidos()
    {
        yield return [new DeductStockRequest(Guid.Empty, [new(ProductA, 1)])];
        yield return [new DeductStockRequest(Guid.NewGuid(), [])];
        yield return [new DeductStockRequest(Guid.NewGuid(), null!)];
        yield return [new DeductStockRequest(Guid.NewGuid(), [new(Guid.Empty, 1)])];
        yield return [new DeductStockRequest(Guid.NewGuid(), [new(ProductA, 0)])];
    }

    [Theory]
    [MemberData(nameof(PedidosInvalidos))]
    public async Task Baixar_rejeita_identificadores_ou_itens_invalidos(DeductStockRequest request)
    {
        var store = new FakeStockStore();

        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            new StockDeductionService(store).DeductAsync(request, default));

        Assert.Equal("VALIDATION_ERROR", error.Code);
        Assert.Empty(store.Deductions);
    }

    [Fact]
    public async Task Baixar_rejeita_soma_de_quantidades_acima_do_limite()
    {
        var store = new FakeStockStore();
        var request = new DeductStockRequest(Guid.NewGuid(), [new(ProductA, int.MaxValue), new(ProductA, 1)]);

        await Assert.ThrowsAsync<BusinessException>(() => new StockDeductionService(store).DeductAsync(request, default));

        Assert.Empty(store.Deductions);
    }

    [Fact]
    public async Task Baixar_consolida_itens_e_mantem_hash_igual_para_o_mesmo_pedido()
    {
        var store = new FakeStockStore();
        var service = new StockDeductionService(store);
        var invoiceId = Guid.NewGuid();

        await service.DeductAsync(new(invoiceId, [new(ProductB, 2), new(ProductA, 1), new(ProductA, 3)]), default);
        await service.DeductAsync(new(invoiceId, [new(ProductA, 4), new(ProductB, 2)]), default);

        Assert.Equal(new DeductionItem(ProductA, 4), store.Deductions[0].Request.Items[0]);
        Assert.Equal(new DeductionItem(ProductB, 2), store.Deductions[0].Request.Items[1]);
        Assert.Equal(2, store.Deductions[0].Request.Items.Count);
        Assert.Equal(store.Deductions[0].Hash, store.Deductions[1].Hash);
        Assert.Equal(64, store.Deductions[0].Hash.Length);
    }

    [Fact]
    public async Task Baixar_gera_hash_diferente_para_notas_diferentes()
    {
        var store = new FakeStockStore();
        var service = new StockDeductionService(store);

        await service.DeductAsync(new(Guid.NewGuid(), [new(ProductA, 1)]), default);
        await service.DeductAsync(new(Guid.NewGuid(), [new(ProductA, 1)]), default);

        Assert.NotEqual(store.Deductions[0].Hash, store.Deductions[1].Hash);
    }

    [Fact]
    public async Task Baixar_preserva_erro_de_saldo_insuficiente_da_persistencia()
    {
        var expected = new BusinessException(422, "INSUFFICIENT_STOCK", "Saldo insuficiente.");
        var store = new FakeStockStore { DeductionError = expected };

        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            new StockDeductionService(store).DeductAsync(new(Guid.NewGuid(), [new(ProductA, 2)]), default));

        Assert.Same(expected, error);
        Assert.Single(store.Deductions);
    }

    [Fact]
    public void Validar_estoque_aceita_saldo_exatamente_igual_a_quantidade()
    {
        var product = new Product { Id = ProductA, Code = "P001", Balance = 2 };

        var error = Record.Exception(() => StockDeductionService.ValidateAvailableStock(product, new(ProductA, 2)));

        Assert.Null(error);
        Assert.Equal(2, product.Balance); // A validação não altera o saldo.
    }

    [Fact]
    public void Validar_estoque_rejeita_saldo_menor_que_quantidade()
    {
        var product = new Product { Id = ProductA, Code = "P001", Balance = 1 };

        var error = Assert.Throws<BusinessException>(() =>
            StockDeductionService.ValidateAvailableStock(product, new(ProductA, 2)));

        Assert.Equal(422, error.StatusCode);
        Assert.Equal("INSUFFICIENT_STOCK", error.Code);
        Assert.Equal(1, product.Balance);
    }

    [Fact]
    public void Validar_estoque_rejeita_produto_inexistente()
    {
        var error = Assert.Throws<BusinessException>(() =>
            StockDeductionService.ValidateAvailableStock(null, new(ProductA, 1)));

        Assert.Equal(422, error.StatusCode);
        Assert.Equal("PRODUCT_NOT_FOUND", error.Code);
    }
}
