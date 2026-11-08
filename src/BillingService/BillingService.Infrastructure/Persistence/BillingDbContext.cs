using BillingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BillingService.Infrastructure.Persistence;

public sealed class BillingDbContext(DbContextOptions<BillingDbContext> options)
    : DbContext(options)
{
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<InvoiceSequence> InvoiceSequence => Set<InvoiceSequence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BillingDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
