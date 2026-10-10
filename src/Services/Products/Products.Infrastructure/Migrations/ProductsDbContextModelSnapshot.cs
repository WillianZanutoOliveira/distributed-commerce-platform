using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Products.Domain;

#nullable disable

namespace Products.Infrastructure.Migrations;

[DbContext(typeof(ProductsDbContext))]
public sealed class ProductsDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.12")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);
        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Id).ValueGeneratedNever();
            entity.Property(p => p.Sku).HasMaxLength(64).IsRequired();
            entity.HasIndex(p => p.Sku).IsUnique();
            entity.Property(p => p.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(p => p.Name);
            entity.Property(p => p.Description).HasMaxLength(2000);
            entity.Property(p => p.Category).HasMaxLength(100).IsRequired();
            entity.HasIndex(p => p.Category);
            entity.Property(p => p.Brand).HasMaxLength(100);
            entity.Property(p => p.Barcode).HasMaxLength(14);
            entity.HasIndex(p => p.Barcode).IsUnique();
            entity.Property(p => p.Unit).HasMaxLength(10).IsRequired();
            entity.Property(p => p.Price).HasPrecision(18, 2);
            entity.Property(p => p.Currency).HasMaxLength(3).IsRequired();
            entity.Property(p => p.IsActive).HasColumnType("boolean");
            entity.Property(p => p.CreatedAt).HasColumnType("timestamp with time zone");
            entity.Property(p => p.UpdatedAt).HasColumnType("timestamp with time zone");
        });
    }
}
