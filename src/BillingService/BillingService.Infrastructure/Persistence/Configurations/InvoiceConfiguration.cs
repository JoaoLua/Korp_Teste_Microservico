using BillingService.Domain.Entities;
using BillingService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillingService.Infrastructure.Persistence.Configurations;

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices", table =>
        {
            table.HasCheckConstraint("CK_Invoices_Number", "[Number] > 0");
            table.HasCheckConstraint("CK_Invoices_Status", "[Status] IN (1, 2)");
            table.HasCheckConstraint(
                "CK_Invoices_Status_ClosedAt",
                "([Status] = 1 AND [ClosedAt] IS NULL) OR " +
                "([Status] = 2 AND [ClosedAt] IS NOT NULL)");
        });

        builder.HasKey(invoice => invoice.Id).HasName("PK_Invoices");

        builder.Property(invoice => invoice.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()")
            .ValueGeneratedOnAdd();
        builder.Property(invoice => invoice.Number).ValueGeneratedNever();
        builder.Property(invoice => invoice.Status)
            .HasConversion<byte>()
            .HasDefaultValue(InvoiceStatus.Open);
        builder.Property(invoice => invoice.CreatedAt)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(invoice => invoice.ClosedAt).HasColumnType("datetime2");
        builder.Property(invoice => invoice.UpdatedAt).HasColumnType("datetime2");
        builder.Property(invoice => invoice.RowVersion).IsRowVersion();

        builder.HasIndex(invoice => invoice.Number)
            .IsUnique()
            .HasDatabaseName("UQ_Invoices_Number");
    }
}
