using BillingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillingService.Infrastructure.Persistence.Configurations;

public sealed class InvoiceSequenceConfiguration : IEntityTypeConfiguration<InvoiceSequence>
{
    public void Configure(EntityTypeBuilder<InvoiceSequence> builder)
    {
        builder.ToTable("InvoiceSequence", table =>
        {
            table.HasCheckConstraint("CK_InvoiceSequence_SingleRow", "[Id] = 1");
            table.HasCheckConstraint("CK_InvoiceSequence_CurrentValue", "[CurrentValue] >= 0");
        });

        builder.HasKey(sequence => sequence.Id).HasName("PK_InvoiceSequence");
        builder.Property(sequence => sequence.Id).ValueGeneratedNever();
        builder.Property(sequence => sequence.CurrentValue).HasDefaultValue(0);
        builder.Property(sequence => sequence.UpdatedAt).HasColumnType("datetime2");
    }
}
