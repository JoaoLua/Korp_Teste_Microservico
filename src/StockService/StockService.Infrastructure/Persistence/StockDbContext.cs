using Microsoft.EntityFrameworkCore;
using StockService.Domain.Entities;

namespace StockService.Infrastructure.Persistence;

public sealed class StockDbContext(DbContextOptions<StockDbContext> options)
    : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<StockDeductionOperation> StockDeductionOperations => Set<StockDeductionOperation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StockDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
