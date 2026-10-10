using Microsoft.EntityFrameworkCore;
using Npgsql;
using Products.Application;
using Products.Domain;

namespace Products.Infrastructure;

public sealed class ProductsDbContext(DbContextOptions<ProductsDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
            entity.Property(p => p.Currency).HasMaxLength(3).IsRequired();
            entity.Property(p => p.Price).HasPrecision(18, 2);
        });

        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new DuplicateProductIdentifierException();
        }
    }
}
