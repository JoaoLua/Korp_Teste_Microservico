using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using StockService.Application.Abstractions;
using StockService.Application.Common;
using StockService.Application.Contracts;
using StockService.Application.Services;
using StockService.Domain.Entities;
using StockService.Domain.Enums;

namespace StockService.Infrastructure.Persistence;

public sealed class StockStore(StockDbContext db, ILogger<StockStore> logger) : IStockStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PageResponse<ProductResponse>> ListProductsAsync(string? search, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Products.AsNoTracking();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(product => product.Code.Contains(search) || product.Description.Contains(search));
        var count = await query.CountAsync(ct);
        var products = await query.OrderBy(product => product.Code).ThenBy(product => product.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(products.Select(ToResponse).ToArray(), page, pageSize, count);
    }

    public async Task<ProductResponse?> FindProductAsync(Guid id, CancellationToken ct)
    {
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(product => product.Id == id, ct);
        return product is null ? null : ToResponse(product);
    }

    public async Task<ProductLookupResponse> LookupAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var products = await db.Products.AsNoTracking().Where(product => ids.Contains(product.Id)).ToListAsync(ct);
        var foundIds = products.Select(product => product.Id).ToHashSet();
        return new(products.Select(ToResponse).ToArray(), ids.Where(id => !foundIds.Contains(id)).ToArray());
    }

    public async Task<ProductResponse> CreateProductAsync(CreateProductRequest request, CancellationToken ct)
    {
        var product = new Product
        {
            Code = request.Code, Description = request.Description, Balance = request.Balance,
            MinimumStock = request.MinimumStock, Category = request.Category, CreatedAt = DateTime.UtcNow
        };
        if (request.Balance > 0)
            product.StockMovements.Add(new StockMovement
            {
                Product = product, Quantity = request.Balance, MovementType = StockMovementType.Entry,
                Reason = "Saldo inicial do cadastro", CreatedAt = product.CreatedAt
            });
        db.Products.Add(product);
        // EF commits both the product and initial movement in one local transaction.
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Product {ProductId} created with balance {Balance}", product.Id, product.Balance);
        return ToResponse(product);
    }

    public async Task<ProductResponse> UpdateProductAsync(Guid id, UpdateProductRequest request, byte[] expectedVersion, CancellationToken ct)
    {
        var product = await db.Products.SingleOrDefaultAsync(product => product.Id == id, ct)
            ?? throw ProductService.NotFound();
        RequireVersion(product, expectedVersion);
        db.Entry(product).Property(p => p.RowVersion).OriginalValue = expectedVersion;
        product.Code = request.Code;
        product.Description = request.Description;
        product.MinimumStock = request.MinimumStock;
        product.Category = request.Category;
        product.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToResponse(product);
    }

    public async Task<ProductResponse> AdjustStockAsync(Guid id, AdjustStockRequest request, byte[] expectedVersion, CancellationToken ct)
    {
        var product = await db.Products.SingleOrDefaultAsync(product => product.Id == id, ct)
            ?? throw ProductService.NotFound();
        RequireVersion(product, expectedVersion);
        var delta = request.Balance - product.Balance;
        if (delta == 0) return ToResponse(product);

        db.Entry(product).Property(p => p.RowVersion).OriginalValue = expectedVersion;
        var now = DateTime.UtcNow;
        product.Balance = request.Balance;
        product.UpdatedAt = now;
        db.StockMovements.Add(new StockMovement
        {
            ProductId = id, Quantity = Math.Abs(delta),
            MovementType = delta > 0 ? StockMovementType.Entry : StockMovementType.Exit,
            Reason = request.Reason, CreatedAt = now
        });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Product {ProductId} adjusted by {Quantity}, resulting balance {Balance}", id, delta, product.Balance);
        return ToResponse(product);
    }

    public async Task<PageResponse<MovementResponse>> ListMovementsAsync(Guid productId, int page, int pageSize, CancellationToken ct)
    {
        if (!await db.Products.AnyAsync(product => product.Id == productId, ct)) throw ProductService.NotFound();
        var query = db.StockMovements.AsNoTracking().Where(movement => movement.ProductId == productId);
        var count = await query.CountAsync(ct);
        var movements = await query.OrderByDescending(movement => movement.CreatedAt).ThenByDescending(movement => movement.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(movements.Select(movement => new MovementResponse(movement.Id, movement.ProductId, movement.Quantity,
            movement.MovementType.ToString(), movement.Reason, movement.InvoiceId, Utc(movement.CreatedAt))).ToArray(), page, pageSize, count);
    }

    public async Task<DeductionResponse?> FindDeductionAsync(Guid invoiceId, CancellationToken ct)
    {
        var operation = await db.StockDeductionOperations.AsNoTracking()
            .SingleOrDefaultAsync(operation => operation.InvoiceId == invoiceId, ct);
        if (operation is null || operation.Status == StockOperationStatus.Failed) return null;
        if (operation.Status != StockOperationStatus.Completed) throw LegacyOperationPending();
        return ReadResult(operation);
    }

    public async Task<DeductionResponse> DeductAsync(DeductStockRequest request, string requestHash, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var key = request.InvoiceId.ToString("D");

        // Uma nota só pode baixar uma vez, inclusive se duas requisições chegarem juntas.
        await AcquireLockAsync($"stock:invoice:{request.InvoiceId:D}", ct);

        var operation = await db.StockDeductionOperations
            .SingleOrDefaultAsync(operation => operation.InvoiceId == request.InvoiceId, ct);
        if (operation is not null)
        {
            // O formato anterior incluía operationId no hash. Compare os itens para preservar o histórico.
            var previous = ReadStoredRequest(operation);
            if (previous.InvoiceId != request.InvoiceId || !previous.Items.SequenceEqual(request.Items))
                throw new BusinessException(409, "IDEMPOTENCY_CONFLICT", "Esta nota já está associada a outros itens ou quantidades.");

            if (operation.Status == StockOperationStatus.Completed)
                return ReadResult(operation);
            if (operation.Status != StockOperationStatus.Failed)
                throw LegacyOperationPending();
            // Falhas antigas eram gravadas sem débito; uma nova impressão pode reavaliar o saldo.
        }

        // Bloqueie e valide todos os produtos antes de alterar qualquer saldo.
        // A ordem fixa evita que duas notas bloqueiem os mesmos produtos em ordens opostas.
        foreach (var item in request.Items)
        {
            var product = await db.Products
                .FromSqlInterpolated($"SELECT * FROM dbo.Products WITH (UPDLOCK, ROWLOCK) WHERE Id = {item.ProductId}")
                .AsNoTracking().SingleOrDefaultAsync(ct);
            StockDeductionService.ValidateAvailableStock(product, item);
        }

        var now = DateTime.UtcNow;
        foreach (var item in request.Items)
        {
            var affected = await db.Products.Where(product => product.Id == item.ProductId && product.Balance >= item.Quantity)
                .ExecuteUpdateAsync(setters => setters.SetProperty(product => product.Balance, product => product.Balance - item.Quantity)
                    .SetProperty(product => product.UpdatedAt, now), ct);
            if (affected != 1)
                throw new BusinessException(409, "STOCK_CONCURRENCY", "O estoque foi alterado durante a baixa. Tente imprimir novamente.");
            db.StockMovements.Add(new StockMovement
            {
                ProductId = item.ProductId, Quantity = item.Quantity, MovementType = StockMovementType.Exit,
                Reason = "Baixa de nota fiscal", InvoiceId = request.InvoiceId, IdempotencyKey = key, CreatedAt = now
            });
        }

        var result = new DeductionResponse(request.InvoiceId, "Completed", now, request.Items);
        if (operation is null)
        {
            operation = new StockDeductionOperation
            {
                IdempotencyKey = key, InvoiceId = request.InvoiceId, CreatedAt = now
            };
            db.StockDeductionOperations.Add(operation);
        }
        operation.Status = StockOperationStatus.Completed;
        operation.RequestHash = requestHash;
        operation.RequestPayload = JsonSerializer.Serialize(request, JsonOptions);
        operation.CompletedAt = now;
        operation.ResponsePayload = JsonSerializer.Serialize(result, JsonOptions);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        logger.LogInformation("Estoque baixado para a nota {InvoiceId}", request.InvoiceId);
        return result;
    }

    private async Task AcquireLockAsync(string resource, CancellationToken ct)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = "DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 3000; SELECT @result;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@resource";
        parameter.DbType = DbType.String;
        parameter.Size = 255;
        parameter.Value = resource;
        command.Parameters.Add(parameter);
        var result = Convert.ToInt32(await command.ExecuteScalarAsync(ct));
        if (result < 0)
            throw new BusinessException(503, "OPERATION_BUSY", "Há outra requisição para esta nota. Tente imprimir novamente.");
    }

    private static DeductionResponse ReadResult(StockDeductionOperation operation) => new(
        operation.InvoiceId, "Completed", Utc(operation.CompletedAt ?? throw LegacyOperationPending()),
        ReadStoredRequest(operation).Items);

    private static DeductStockRequest ReadStoredRequest(StockDeductionOperation operation)
    {
        try
        {
            var request = JsonSerializer.Deserialize<DeductStockRequest>(operation.RequestPayload, JsonOptions);
            if (request is null || request.InvoiceId != operation.InvoiceId) throw LegacyOperationPending();
            return StockDeductionService.Normalize(request);
        }
        catch (JsonException) { throw LegacyOperationPending(); }
        catch (BusinessException exception) when (exception.StatusCode == 400) { throw LegacyOperationPending(); }
    }

    private static BusinessException LegacyOperationPending() => new(409, "OPERATION_REVIEW_REQUIRED",
        "Existe um registro antigo sem resultado confirmado. Verifique a baixa antes de imprimir novamente.");

    private static void RequireVersion(Product product, byte[] expected)
    {
        if (!product.RowVersion.SequenceEqual(expected))
            throw new BusinessException(409, "ROW_VERSION_CONFLICT", "O produto foi alterado. Atualize os dados antes de tentar novamente.");
    }

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static ProductResponse ToResponse(Product product) => new(product.Id, product.Code, product.Description,
        product.Balance, product.MinimumStock, product.Category, Utc(product.CreatedAt),
        product.UpdatedAt.HasValue ? Utc(product.UpdatedAt.Value) : null, Convert.ToBase64String(product.RowVersion));
}
