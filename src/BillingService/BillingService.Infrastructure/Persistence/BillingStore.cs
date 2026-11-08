using BillingService.Application.Abstractions;
using BillingService.Application.Contracts;
using BillingService.Domain.Entities;
using BillingService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BillingService.Infrastructure.Persistence;

public sealed class BillingStore(BillingDbContext db) : IBillingStore
{
    public async Task<PageResponse<InvoiceResponse>> ListAsync(int page, int pageSize, long? number, InvoiceStatus? status, CancellationToken cancellationToken)
    {
        var query = db.Invoices.AsNoTracking();
        if (number.HasValue) query = query.Where(invoice => invoice.Number == number);
        if (status.HasValue) query = query.Where(invoice => invoice.Status == status);
        var count = await query.LongCountAsync(cancellationToken);
        var invoices = await query.Include(invoice => invoice.Items)
            .OrderByDescending(invoice => invoice.Number).Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PageResponse<InvoiceResponse>(invoices.Select(ToResponse).ToArray(), page, pageSize, count);
    }

    public async Task<InvoiceResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var invoice = await db.Invoices.AsNoTracking().Include(invoice => invoice.Items)
            .SingleOrDefaultAsync(invoice => invoice.Id == id, cancellationToken)
            ?? throw new BillingException(404, "INVOICE_NOT_FOUND", "Nota não encontrada.");
        return ToResponse(invoice);
    }

    public async Task<InvoiceResponse> CreateAsync(IReadOnlyList<InvoiceItemRequest> items, IReadOnlyList<StockProduct> products, CancellationToken cancellationToken)
    {
        var snapshots = products.ToDictionary(product => product.Id);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // A sequência e a nota usam a mesma transação: uma falha não consome o número.
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "UPDATE dbo.InvoiceSequence WITH (UPDLOCK, HOLDLOCK) SET CurrentValue = CurrentValue + 1, UpdatedAt = SYSUTCDATETIME() OUTPUT INSERTED.CurrentValue WHERE Id = 1;";
        var nextNumber = await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Execute os scripts para inicializar InvoiceSequence.");
        var now = DateTime.UtcNow;
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(), Number = Convert.ToInt64(nextNumber), Status = InvoiceStatus.Open, CreatedAt = now,
            Items = items.Select(item => new InvoiceItem
            {
                Id = Guid.NewGuid(), ProductId = item.ProductId, Quantity = item.Quantity,
                ProductCode = snapshots[item.ProductId].Code, ProductDescription = snapshots[item.ProductId].Description, CreatedAt = now
            }).ToArray()
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(invoice);
    }

    public async Task<InvoiceResponse> CloseAsync(Guid invoiceId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // Duas impressões podem confirmar a mesma baixa. O lock serializa apenas o fechamento local.
        var invoice = await db.Invoices
            .FromSqlInterpolated($"SELECT * FROM dbo.Invoices WITH (UPDLOCK) WHERE Id = {invoiceId}")
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new BillingException(404, "INVOICE_NOT_FOUND", "Nota não encontrada.");
        if (invoice.Status == InvoiceStatus.Open)
        {
            invoice.Status = InvoiceStatus.Closed;
            invoice.ClosedAt = DateTime.UtcNow;
            invoice.UpdatedAt = invoice.ClosedAt;
            await db.SaveChangesAsync(cancellationToken);
        }
        invoice.Items = await db.InvoiceItems.AsNoTracking()
            .Where(item => item.InvoiceId == invoiceId).ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(invoice);
    }

    private static InvoiceResponse ToResponse(Invoice invoice) => new(invoice.Id, invoice.Number, invoice.Status,
        DateTime.SpecifyKind(invoice.CreatedAt, DateTimeKind.Utc),
        invoice.ClosedAt.HasValue ? DateTime.SpecifyKind(invoice.ClosedAt.Value, DateTimeKind.Utc) : null,
        invoice.Items.OrderBy(item => item.ProductCode).ThenBy(item => item.ProductId)
            .Select(item => new InvoiceItemResponse(item.Id, item.ProductId, item.ProductCode, item.ProductDescription, item.Quantity)).ToArray());
}
