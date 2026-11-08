using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockService.Domain.Entities;

namespace StockService.Infrastructure.Persistence.Configurations;

public sealed class StockDeductionOperationConfiguration
    : IEntityTypeConfiguration<StockDeductionOperation>
{
    public void Configure(EntityTypeBuilder<StockDeductionOperation> builder)
    {
        builder.ToTable("StockDeductionOperations", table =>
        {
            table.HasCheckConstraint(
                "CK_StockDeductionOperations_IdempotencyKey_NotBlank",
                "LEN(LTRIM(RTRIM([IdempotencyKey]))) > 0");
            table.HasCheckConstraint("CK_StockDeductionOperations_Status", "[Status] = 2");
            table.HasCheckConstraint("CK_StockDeductionOperations_RequestHash", "LEN([RequestHash]) = 64");
            table.HasCheckConstraint("CK_StockDeductionOperations_RequestPayload_Json", "ISJSON([RequestPayload]) = 1");
            table.HasCheckConstraint(
                "CK_StockDeductionOperations_ResponsePayload_Json",
                "[ResponsePayload] IS NULL OR ISJSON([ResponsePayload]) = 1");
            table.HasCheckConstraint(
                "CK_StockDeductionOperations_Completion",
                "[Status] = 2 AND [CompletedAt] IS NOT NULL");
        });

        builder.HasKey(operation => operation.Id).HasName("PK_StockDeductionOperations");

        builder.Property(operation => operation.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()")
            .ValueGeneratedOnAdd();
        builder.Property(operation => operation.IdempotencyKey).HasMaxLength(100).IsRequired();
        builder.Property(operation => operation.Status).HasConversion<byte>();
        builder.Property(operation => operation.RequestPayload).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(operation => operation.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(operation => operation.ResponsePayload).HasColumnType("nvarchar(max)");
        builder.Property(operation => operation.CreatedAt)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(operation => operation.CompletedAt).HasColumnType("datetime2");

        builder.HasIndex(operation => operation.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("UQ_StockDeductionOperations_IdempotencyKey");
        builder.HasIndex(operation => operation.InvoiceId)
            .IsUnique()
            .HasDatabaseName("UQ_StockDeductionOperations_InvoiceId");
    }
}
