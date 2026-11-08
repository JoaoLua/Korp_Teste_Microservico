using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BillingService.Application.Abstractions;
using BillingService.Application.Contracts;

namespace BillingService.Infrastructure.Http;

public sealed class StockClient(HttpClient http) : IStockClient
{
    public async Task<ProductLookupResponse> LookupAsync(IReadOnlyList<Guid> ids, string correlationId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            "api/products/lookup?" + string.Join("&", ids.Select(id => $"ids={id:D}")));
        request.Headers.Add("X-Correlation-ID", correlationId);
        using var response = await SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) throw Unavailable();
        var result = await ReadAsync<ProductLookupResponse>(response, cancellationToken);
        if (result.Products is null || result.MissingIds is null
            || result.Products.Any(product => product is null || product.Id == Guid.Empty
                || string.IsNullOrWhiteSpace(product.Code) || product.Code.Length > 50
                || string.IsNullOrWhiteSpace(product.Description) || product.Description.Length > 200)
            || !result.Products.Select(product => product.Id).Concat(result.MissingIds).Order().SequenceEqual(ids.Order()))
            throw ContractError();
        return result;
    }

    public async Task<StockDeductionResponse> DeductAsync(StockDeductionRequest body, string correlationId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/stock/deduct")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("X-Correlation-ID", correlationId);
        using var response = await SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var problem = await ReadAsync<StockProblem>(response, cancellationToken);
            throw new BillingException(409, problem.Code == "STOCK_CONCURRENCY" ? "STOCK_CONCURRENCY" : "STOCK_OPERATION_CONFLICT",
                problem.Code == "STOCK_CONCURRENCY"
                    ? "O estoque mudou durante a baixa. Consulte a nota e tente novamente."
                    : "Existe um conflito no registro da baixa. Verifique os itens e o histórico da nota antes de tentar novamente.");
        }
        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            var problem = await ReadAsync<StockProblem>(response, cancellationToken);
            throw problem.Code switch
            {
                "INSUFFICIENT_STOCK" => new BillingException(422, "INSUFFICIENT_STOCK", "Estoque insuficiente para imprimir a nota. Nenhum item foi baixado."),
                "PRODUCT_NOT_FOUND" => new BillingException(422, "PRODUCT_NOT_FOUND", "Um produto da nota não existe no estoque. Nenhum item foi baixado."),
                _ => ContractError()
            };
        }
        if (!response.IsSuccessStatusCode) throw Unavailable();
        var result = await ReadAsync<StockDeductionResponse>(response, cancellationToken);
        if (response.StatusCode != HttpStatusCode.OK || result.Status != "Completed" || result.InvoiceId != body.InvoiceId
            || result.CompletedAt == default || result.Items is null || result.Items.Any(item => item is null)
            || !result.Items.OrderBy(item => item.ProductId).SequenceEqual(body.Items.OrderBy(item => item.ProductId)))
            throw ContractError();
        return result;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await http.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new StockTransportException("STOCK_TIMEOUT", "O estoque não respondeu a tempo.");
        }
        catch (HttpRequestException)
        {
            throw Unavailable();
        }
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try { return await response.Content.ReadFromJsonAsync<T>(cancellationToken) ?? throw ContractError(); }
        catch (JsonException) { throw ContractError(); }
        catch (NotSupportedException) { throw ContractError(); }
    }

    private static StockTransportException Unavailable() => new("STOCK_UNAVAILABLE", "Não foi possível comunicar com o estoque.");
    private static StockTransportException ContractError() => new("STOCK_CONTRACT_ERROR", "O estoque devolveu uma resposta incompatível.");
    private sealed record StockProblem(string? Code);
}
