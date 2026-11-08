using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using StockService.Application.Abstractions;
using StockService.Application.Common;
using StockService.Application.Contracts;
using StockService.Domain.Entities;

namespace StockService.Application.Services;

public sealed class StockDeductionService(IStockStore store)
{
    public Task<DeductionResponse> DeductAsync(DeductStockRequest request, CancellationToken ct)
    {
        request = Normalize(request);
        var canonical = new StringBuilder(request.InvoiceId.ToString("D"));
        foreach (var item in request.Items)
            canonical.Append('|').Append(item.ProductId.ToString("D")).Append(':')
                .Append(item.Quantity.ToString(CultureInfo.InvariantCulture));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
        return store.DeductAsync(request, hash, ct);
    }

    public static DeductStockRequest Normalize(DeductStockRequest request)
    {
        if (request.InvoiceId == Guid.Empty)
            throw BusinessException.Validation("invoiceId", "Informe um invoiceId válido.");
        if (request.Items is null || request.Items.Count is < 1 or > 1000 ||
            request.Items.Any(item => item is null || item.ProductId == Guid.Empty || item.Quantity < 1))
            throw BusinessException.Validation("items", "Informe itens com IDs válidos e quantidades positivas (máximo de 1.000 linhas).");

        var groups = request.Items.GroupBy(item => item.ProductId).ToArray();
        if (groups.Length > 100)
            throw BusinessException.Validation("items", "A nota admite até 100 produtos distintos.");
        var items = groups.Select(group =>
        {
            var quantity = group.Sum(item => (long)item.Quantity);
            if (quantity > int.MaxValue)
                throw BusinessException.Validation("items", "A soma das quantidades excede o limite permitido.");
            return new DeductionItem(group.Key, (int)quantity);
        }).OrderBy(item => item.ProductId.ToString("D"), StringComparer.Ordinal).ToArray();

        return request with { Items = items };
    }

    public static void ValidateAvailableStock(Product? product, DeductionItem item)
    {
        if (product is null)
            throw new BusinessException(422, "PRODUCT_NOT_FOUND", $"Produto {item.ProductId:D} não encontrado.");
        if (product.Balance < item.Quantity)
            throw new BusinessException(422, "INSUFFICIENT_STOCK",
                $"Saldo insuficiente para o produto {product.Code}. Disponível: {product.Balance}; solicitado: {item.Quantity}.");
    }

    public async Task<DeductionResponse> GetAsync(Guid invoiceId, CancellationToken ct) =>
        await store.FindDeductionAsync(invoiceId, ct)
        ?? throw new BusinessException(404, "DEDUCTION_NOT_FOUND", "Nenhuma baixa registrada para esta nota.");
}
