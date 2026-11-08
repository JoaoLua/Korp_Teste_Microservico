using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockService.Domain.Entities;

namespace StockService.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", table =>
        {
            table.HasCheckConstraint("CK_Products_Code_NotBlank", "LEN(LTRIM(RTRIM([Code]))) > 0");
            table.HasCheckConstraint("CK_Products_Description_NotBlank", "LEN(LTRIM(RTRIM([Description]))) > 0");
            table.HasCheckConstraint("CK_Products_Balance", "[Balance] >= 0");
            table.HasCheckConstraint("CK_Products_MinimumStock", "[MinimumStock] >= 0");
        });

        builder.HasKey(product => product.Id).HasName("PK_Products");

        builder.Property(product => product.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()")
            .ValueGeneratedOnAdd();
        builder.Property(product => product.Code).HasMaxLength(50).IsRequired();
        builder.Property(product => product.Description).HasMaxLength(200).IsRequired();
        builder.Property(product => product.Balance).HasDefaultValue(0);
        builder.Property(product => product.MinimumStock).HasDefaultValue(0);
        builder.Property(product => product.Category).HasMaxLength(100);
        builder.Property(product => product.CreatedAt)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(product => product.UpdatedAt).HasColumnType("datetime2");
        builder.Property(product => product.RowVersion).IsRowVersion();

        builder.HasIndex(product => product.Code)
            .IsUnique()
            .HasDatabaseName("UQ_Products_Code");
    }
}
