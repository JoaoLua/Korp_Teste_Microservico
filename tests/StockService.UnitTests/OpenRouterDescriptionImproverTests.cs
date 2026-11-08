using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using StockService.Application.Common;
using StockService.Infrastructure.AI;
using Xunit;

namespace StockService.UnitTests;

public sealed class OpenRouterDescriptionImproverTests
{
    private const string FakeApiKey = "chave-ficticia-de-teste";
    private const string Model = "nvidia/nemotron-3.5-lightning:free";
    private const string SuccessfulResponse = """
        {"choices":[{"finish_reason":"stop","message":{"content":"Parafuso de aço de 10 mm."}}]}
        """;

    [Fact]
    public async Task Melhorar_envia_descricao_com_chave_no_header_e_limite_de_preco_zero()
    {
        var handler = new FakeHandler(async (request, cancellationToken) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://openrouter.ai/api/v1/chat/completions", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal(FakeApiKey, request.Headers.Authorization.Parameter);
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Assert.DoesNotContain(FakeApiKey, body);
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            Assert.Equal(Model, root.GetProperty("model").GetString());
            Assert.Equal(256, root.GetProperty("max_tokens").GetInt32());
            Assert.False(root.GetProperty("reasoning").GetProperty("enabled").GetBoolean());
            var maxPrice = root.GetProperty("provider").GetProperty("max_price");
            Assert.Equal(0m, maxPrice.GetProperty("prompt").GetDecimal());
            Assert.Equal(0m, maxPrice.GetProperty("completion").GetDecimal());
            Assert.Equal(0m, maxPrice.GetProperty("request").GetDecimal());
            var messages = root.GetProperty("messages");
            Assert.Equal(2, messages.GetArrayLength());
            Assert.Equal("system", messages[0].GetProperty("role").GetString());
            Assert.False(string.IsNullOrWhiteSpace(messages[0].GetProperty("content").GetString()));
            Assert.Equal("user", messages[1].GetProperty("role").GetString());
            Assert.Equal("parafuso aço 10mm", messages[1].GetProperty("content").GetString());
            return JsonResponse(SuccessfulResponse);
        });
        using var client = new HttpClient(handler);

        var result = await CreateImprover(client).ImproveAsync("parafuso aço 10mm", default);

        Assert.Equal("Parafuso de aço de 10 mm.", result);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("", Model)]
    [InlineData(FakeApiKey, " ")]
    public async Task Melhorar_sem_configuracao_nao_envia_requisicao(string apiKey, string model)
    {
        var handler = new FakeHandler((_, _) => Task.FromResult(JsonResponse(SuccessfulResponse)));
        using var client = new HttpClient(handler);
        var improver = new OpenRouterDescriptionImprover(client,
            Options.Create(new OpenRouterOptions { ApiKey = apiKey, Model = model }));

        var error = await Assert.ThrowsAsync<BusinessException>(() => improver.ImproveAsync("Produto", default));

        Assert.Equal(503, error.StatusCode);
        Assert.Equal("AI_NOT_CONFIGURED", error.Code);
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "AI_RATE_LIMITED")]
    [InlineData(HttpStatusCode.Unauthorized, "AI_UNAVAILABLE")]
    [InlineData(HttpStatusCode.InternalServerError, "AI_UNAVAILABLE")]
    public async Task Melhorar_traduz_erro_sem_repetir_chamada_ou_expor_resposta(HttpStatusCode status, string code)
    {
        const string privateProviderDetail = "detalhe-privado-do-provedor";
        var handler = new FakeHandler((_, _) => Task.FromResult(JsonResponse(privateProviderDetail, status)));
        using var client = new HttpClient(handler);

        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            CreateImprover(client).ImproveAsync("Produto", default));

        Assert.Equal(503, error.StatusCode);
        Assert.Equal(code, error.Code);
        Assert.DoesNotContain(privateProviderDetail, error.ToString());
        Assert.DoesNotContain(FakeApiKey, error.ToString());
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("resposta que não é JSON")]
    [InlineData("{\"choices\":[]}")]
    [InlineData("{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":{\"text\":\"Produto\"}}}]}")]
    [InlineData("{\"choices\":[{\"finish_reason\":\"length\",\"message\":{\"content\":\"Produto incompleto\"}}]}")]
    public async Task Melhorar_rejeita_resposta_malformada_ou_incompleta(string response)
    {
        var handler = new FakeHandler((_, _) => Task.FromResult(JsonResponse(response)));
        using var client = new HttpClient(handler);

        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            CreateImprover(client).ImproveAsync("Produto", default));

        Assert.Equal(502, error.StatusCode);
        Assert.Equal("AI_INVALID_RESPONSE", error.Code);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Melhorar_traduz_falha_de_rede_sem_expor_detalhes()
    {
        var handler = new FakeHandler((_, _) => throw new HttpRequestException("detalhe-privado-do-provedor"));
        using var client = new HttpClient(handler);

        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            CreateImprover(client).ImproveAsync("Produto", default));

        Assert.Equal(503, error.StatusCode);
        Assert.Equal("AI_UNAVAILABLE", error.Code);
        Assert.DoesNotContain("detalhe-privado-do-provedor", error.ToString());
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Melhorar_traduz_timeout_em_indisponibilidade()
    {
        var handler = new FakeHandler((_, _) => throw new TaskCanceledException("Timeout"));
        using var client = new HttpClient(handler);

        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            CreateImprover(client).ImproveAsync("Produto", default));

        Assert.Equal(503, error.StatusCode);
        Assert.Equal("AI_TIMEOUT", error.Code);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Melhorar_preserva_cancelamento_solicitado_pelo_cliente()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new FakeHandler((_, cancellationToken) =>
        {
            cancellation.Cancel();
            return Task.FromCanceled<HttpResponseMessage>(cancellationToken);
        });
        using var client = new HttpClient(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateImprover(client).ImproveAsync("Produto", cancellation.Token));

        Assert.Equal(1, handler.Calls);
    }

    private static OpenRouterDescriptionImprover CreateImprover(HttpClient client) =>
        new(client, Options.Create(new OpenRouterOptions { ApiKey = FakeApiKey, Model = Model }));

    private static HttpResponseMessage JsonResponse(string content, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(content, Encoding.UTF8, "application/json") };

    private sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return send(request, cancellationToken);
        }
    }
}
