using StockService.Application.Common;
using StockService.Application.Contracts;
using StockService.Application.Services;
using Xunit;

namespace StockService.UnitTests;

public sealed class ProductServiceTests
{
    [Fact]
    public async Task Criar_remove_espacos_antes_de_salvar()
    {
        var store = new FakeStockStore();
        var service = new ProductService(store);

        var result = await service.CreateAsync(new(" P001 ", " Produto de teste ", 10, 2, " Categoria "), default);

        Assert.Equal(new CreateProductRequest("P001", "Produto de teste", 10, 2, "Categoria"), store.CreatedProduct);
        Assert.Equal("P001", result.Code);
    }

    [Theory]
    [InlineData(" ", "Produto", "code")]
    [InlineData("P001", " ", "description")]
    public async Task Criar_rejeita_textos_obrigatorios_vazios(string code, string description, string field)
    {
        var store = new FakeStockStore();

        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            new ProductService(store).CreateAsync(new(code, description, 0), default));

        Assert.Equal(400, error.StatusCode);
        Assert.Contains(field, error.Errors!.Keys);
        Assert.Null(store.CreatedProduct);
    }

    [Theory]
    [InlineData(-1, 0, "balance")]
    [InlineData(0, -1, "minimumStock")]
    public async Task Criar_rejeita_saldo_ou_minimo_negativo(int balance, int minimum, string field)
    {
        var store = new FakeStockStore();

        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            new ProductService(store).CreateAsync(new("P001", "Produto", balance, minimum), default));

        Assert.Contains(field, error.Errors!.Keys);
        Assert.Null(store.CreatedProduct);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("nao-e-base64")]
    [InlineData("AQID")]
    public async Task Ajustar_rejeita_versao_ausente_ou_invalida(string? rowVersion)
    {
        var store = new FakeStockStore();

        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            new ProductService(store).AdjustAsync(Guid.NewGuid(), new(10, "Reposição", rowVersion!), default));

        Assert.Contains("rowVersion", error.Errors!.Keys);
        Assert.Null(store.Adjustment);
    }

    [Fact]
    public async Task Ajustar_normaliza_motivo_e_encaminha_versao_recebida()
    {
        var store = new FakeStockStore();
        byte[] version = [0, 0, 0, 0, 0, 0, 0, 7];
        var request = new AdjustStockRequest(20, " Reposição ", Convert.ToBase64String(version));

        var result = await new ProductService(store).AdjustAsync(Guid.NewGuid(), request, default);

        Assert.Equal("Reposição", store.Adjustment!.Reason);
        Assert.Equal(version, store.ExpectedVersion);
        Assert.Equal(20, result.Balance);
    }

    [Fact]
    public async Task Consultar_produto_inexistente_retorna_erro_de_nao_encontrado()
    {
        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            new ProductService(new FakeStockStore()).GetAsync(Guid.NewGuid(), default));

        Assert.Equal(404, error.StatusCode);
        Assert.Equal("PRODUCT_NOT_FOUND", error.Code);
    }
}
