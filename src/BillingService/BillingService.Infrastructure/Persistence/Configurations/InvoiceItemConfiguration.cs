using BillingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillingService.Infrastructure.Persistence.Configurations;

public sealed class InvoiceItemConfiguration : IEntityTypeConfiguration<InvoiceItem>
{
    public void Configure(EntityTypeBuilder<InvoiceItem> builder)
    {
        builder.ToTable("InvoiceItems", table =>
        {
            table.HasCheckConstraint(
                "CK_InvoiceItems_ProductCode_NotBlank",
                "LEN(LTRIM(RTRIM([ProductCode]))) > 0");
            table.HasCheckConstraint(
                "CK_InvoiceItems_ProductDescription_NotBlank",
                "LEN(LTRIM(RTRIM([ProductDescription]))) > 0");
            table.HasCheckConstraint("CK_InvoiceItems_Quantity", "[Quantity] > 0");
        });

        builder.HasKey(item => item.Id).HasName("PK_InvoiceItems");

        builder.Property(item => item.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()")
            .ValueGeneratedOnAdd();
        builder.Property(item => item.ProductCode).HasMaxLength(50).IsRequired();
        builder.Property(item => item.ProductDescription).HasMaxLength(200).IsRequired();
        builder.Property(item => item.CreatedAt)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(item => item.Invoice)
            .WithMany(invoice => invoice.Items)
            .HasForeignKey(item => item.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_InvoiceItems_Invoices");

        builder.HasIndex(item => new { item.InvoiceId, item.ProductId })
            .IsUnique()
            .HasDatabaseName("UX_InvoiceItems_InvoiceId_ProductId");
        builder.HasIndex(item => item.ProductId)
            .HasDatabaseName("IX_InvoiceItems_ProductId");
    }
}
