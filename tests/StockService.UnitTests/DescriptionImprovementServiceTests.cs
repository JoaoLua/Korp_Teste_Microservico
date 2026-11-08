using StockService.Application.Abstractions;
using StockService.Application.Common;
using StockService.Application.Services;
using Xunit;

namespace StockService.UnitTests;

public sealed class DescriptionImprovementServiceTests
{
    [Fact]
    public async Task Melhorar_normaliza_textos_e_encaminha_cancelamento()
    {
        var improver = new FakeDescriptionImprover(" Parafuso de aço de 10 mm. ");
        using var cancellation = new CancellationTokenSource();

        var result = await new DescriptionImprovementService(improver)
            .ImproveAsync(new(" parafuso aço 10mm "), cancellation.Token);

        Assert.Equal("parafuso aço 10mm", improver.Description);
        Assert.Equal(cancellation.Token, improver.CancellationToken);
        Assert.Equal("Parafuso de aço de 10 mm.", result.Description);
        Assert.Equal(1, improver.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("longa")]
    public async Task Melhorar_rejeita_entrada_invalida_sem_consultar_IA(string? description)
    {
        if (description == "longa") description = new string('a', 201);
        var improver = new FakeDescriptionImprover("Sugestão");

        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            new DescriptionImprovementService(improver).ImproveAsync(new(description)));

        Assert.Equal(400, error.StatusCode);
        Assert.Equal("VALIDATION_ERROR", error.Code);
        Assert.Contains("description", error.Errors!.Keys);
        Assert.Equal(0, improver.Calls);
    }

    [Fact]
    public async Task Melhorar_aceita_limite_de_200_caracteres()
    {
        var description = new string('a', 200);
        var improver = new FakeDescriptionImprover(description);

        var result = await new DescriptionImprovementService(improver).ImproveAsync(new(description));

        Assert.Equal(description, result.Description);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("longa")]
    public async Task Melhorar_rejeita_sugestao_vazia_ou_acima_do_limite(string suggestion)
    {
        if (suggestion == "longa") suggestion = new string('a', 201);
        var improver = new FakeDescriptionImprover(suggestion);

        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            new DescriptionImprovementService(improver).ImproveAsync(new("Produto")));

        Assert.Equal(502, error.StatusCode);
        Assert.Equal("AI_INVALID_RESPONSE", error.Code);
    }

    private sealed class FakeDescriptionImprover(string suggestion) : IDescriptionImprover
    {
        public string? Description { get; private set; }
        public CancellationToken CancellationToken { get; private set; }
        public int Calls { get; private set; }

        public Task<string> ImproveAsync(string description, CancellationToken cancellationToken)
        {
            Description = description;
            CancellationToken = cancellationToken;
            Calls++;
            return Task.FromResult(suggestion);
        }
    }
}
