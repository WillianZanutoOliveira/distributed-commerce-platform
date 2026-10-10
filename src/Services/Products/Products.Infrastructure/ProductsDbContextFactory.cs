using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Products.Infrastructure;

public sealed class ProductsDbContextFactory : IDesignTimeDbContextFactory<ProductsDbContext>
{
    public ProductsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("MIGRATIONS_CONNECTION_STRING")
            ?? "Host=localhost;Port=5436;Database=products;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<ProductsDbContext>().UseNpgsql(connectionString).Options;
        return new ProductsDbContext(options);
    }
}
