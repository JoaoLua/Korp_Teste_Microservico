using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockService.Domain.Entities;

namespace StockService.Infrastructure.Persistence.Configurations;

public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements", table =>
        {
            table.HasCheckConstraint("CK_StockMovements_Quantity", "[Quantity] > 0");
            table.HasCheckConstraint("CK_StockMovements_MovementType", "[MovementType] IN (1, 2, 3)");
            table.HasCheckConstraint(
                "CK_StockMovements_Reversal",
                "([MovementType] = 3 AND [OriginalMovementId] IS NOT NULL) OR " +
                "([MovementType] IN (1, 2) AND [OriginalMovementId] IS NULL)");
            table.HasCheckConstraint(
                "CK_StockMovements_IdempotencyKey_NotBlank",
                "[IdempotencyKey] IS NULL OR LEN(LTRIM(RTRIM([IdempotencyKey]))) > 0");
        });

        builder.HasKey(movement => movement.Id).HasName("PK_StockMovements");

        builder.Property(movement => movement.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()")
            .ValueGeneratedOnAdd();
        builder.Property(movement => movement.MovementType).HasConversion<byte>();
        builder.Property(movement => movement.Reason).HasMaxLength(200);
        builder.Property(movement => movement.IdempotencyKey).HasMaxLength(100);
        builder.Property(movement => movement.CreatedAt)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(movement => movement.Product)
            .WithMany(product => product.StockMovements)
            .HasForeignKey(movement => movement.ProductId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_StockMovements_Products");

        builder.HasOne(movement => movement.OriginalMovement)
            .WithOne(movement => movement.Reversal)
            .HasForeignKey<StockMovement>(movement => movement.OriginalMovementId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_StockMovements_OriginalMovement");

        builder.HasIndex(movement => new { movement.ProductId, movement.CreatedAt })
            .IsDescending(false, true)
            .IncludeProperties(movement => new
            {
                movement.Quantity,
                movement.MovementType,
                movement.InvoiceId
            })
            .HasDatabaseName("IX_StockMovements_ProductId_CreatedAt");
        builder.HasIndex(movement => movement.InvoiceId)
            .HasFilter("[InvoiceId] IS NOT NULL")
            .HasDatabaseName("IX_StockMovements_InvoiceId");
        builder.HasIndex(movement => new
            {
                movement.InvoiceId,
                movement.ProductId,
                movement.MovementType
            })
            .IsUnique()
            .HasFilter("[InvoiceId] IS NOT NULL")
            .HasDatabaseName("UX_StockMovements_Invoice_Product_Movement");
        builder.HasIndex(movement => new { movement.IdempotencyKey, movement.ProductId })
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL")
            .HasDatabaseName("UX_StockMovements_Idempotency_Product");
        builder.HasIndex(movement => movement.OriginalMovementId)
            .IsUnique()
            .HasFilter("[OriginalMovementId] IS NOT NULL")
            .HasDatabaseName("UX_StockMovements_OriginalMovementId");
    }
}
