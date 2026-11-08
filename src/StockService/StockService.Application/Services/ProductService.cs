using StockService.Application.Abstractions;
using StockService.Application.Common;
using StockService.Application.Contracts;

namespace StockService.Application.Services;

public sealed class ProductService(IStockStore store)
{
    public Task<PageResponse<ProductResponse>> ListAsync(string? search, int page, int pageSize, CancellationToken ct)
    {
        ValidatePage(page, pageSize);
        if (search?.Length > 200) throw BusinessException.Validation("search", "Utilize até 200 caracteres.");
        return store.ListProductsAsync(search?.Trim(), page, pageSize, ct);
    }

    public async Task<ProductResponse> GetAsync(Guid id, CancellationToken ct) =>
        await store.FindProductAsync(id, ct) ?? throw NotFound();

    public Task<ProductLookupResponse> LookupAsync(Guid[] ids, CancellationToken ct)
    {
        if (ids.Length is < 1 or > 100 || ids.Contains(Guid.Empty))
            throw BusinessException.Validation("ids", "Informe de 1 a 100 IDs de produtos válidos.");
        return store.LookupAsync(ids.Distinct().ToArray(), ct);
    }

    public Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken ct)
    {
        var code = Required(request.Code, "code", 50);
        var description = Required(request.Description, "description", 200);
        ValidateBalances(request.Balance, request.MinimumStock);
        return store.CreateProductAsync(request with { Code = code, Description = description, Category = Category(request.Category) }, ct);
    }

    public Task<ProductResponse> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct)
    {
        var code = Required(request.Code, "code", 50);
        var description = Required(request.Description, "description", 200);
        ValidateBalances(0, request.MinimumStock);
        var version = RowVersion(request.RowVersion);
        return store.UpdateProductAsync(id, request with { Code = code, Description = description, Category = Category(request.Category) }, version, ct);
    }

    public Task<ProductResponse> AdjustAsync(Guid id, AdjustStockRequest request, CancellationToken ct)
    {
        ValidateBalances(request.Balance, 0);
        var reason = Required(request.Reason, "reason", 200);
        return store.AdjustStockAsync(id, request with { Reason = reason }, RowVersion(request.RowVersion), ct);
    }

    public Task<PageResponse<MovementResponse>> MovementsAsync(Guid id, int page, int pageSize, CancellationToken ct)
    {
        ValidatePage(page, pageSize);
        return store.ListMovementsAsync(id, page, pageSize, ct);
    }

    private static string Required(string? value, string field, int max)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > max)
            throw BusinessException.Validation(field, $"Informe um texto de 1 a {max} caracteres.");
        return value.Trim();
    }

    private static string? Category(string? value) => string.IsNullOrWhiteSpace(value) ? null : Required(value, "category", 100);

    private static byte[] RowVersion(string? value)
    {
        try
        {
            var bytes = Convert.FromBase64String(value ?? "");
            if (bytes.Length == 8) return bytes;
        }
        catch (FormatException) { }
        throw BusinessException.Validation("rowVersion", "Informe a versão de 8 bytes em Base64 recebida na consulta do produto.");
    }

    private static void ValidateBalances(int balance, int minimum)
    {
        if (balance < 0) throw BusinessException.Validation("balance", "O saldo não pode ser negativo.");
        if (minimum < 0) throw BusinessException.Validation("minimumStock", "O estoque mínimo não pode ser negativo.");
    }

    private static void ValidatePage(int page, int size)
    {
        if (page < 1 || size is < 1 or > 100 || (long)(page - 1) * size > int.MaxValue)
            throw BusinessException.Validation("pagination", "Use page positivo e pageSize entre 1 e 100, sem exceder o intervalo permitido.");
    }

    public static BusinessException NotFound() => new(404, "PRODUCT_NOT_FOUND", "Produto não encontrado.");
}
