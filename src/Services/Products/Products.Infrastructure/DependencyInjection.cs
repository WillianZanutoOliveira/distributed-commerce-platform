using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Products.Application;

namespace Products.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddProductsInfrastructure(this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<ProductsDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<ProductsDbContext>());
        return services;
    }
}
