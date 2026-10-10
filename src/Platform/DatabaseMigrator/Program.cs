using Customers.Infrastructure;
using DistributedCommerce.Secrets;
using Inventory.Service;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Orders.Infrastructure;
using Payments.Service;
using Products.Infrastructure;

var target = Environment.GetEnvironmentVariable("MIGRATION_TARGET");

if (string.IsNullOrWhiteSpace(target))
    throw new InvalidOperationException("MIGRATION_TARGET is required.");

var configuration = new ConfigurationManager();
configuration.AddEnvironmentVariables();
await configuration.AddVaultSecretsAsync();

await MigrateAsync(target, configuration);

static async Task MigrateAsync(
    string target,
    IConfiguration configuration)
{
    switch (target.ToLowerInvariant())
    {
        case "customers":
            await MigrateCustomersAsync(RequireConnectionString(configuration, "customers-db"));
            break;

        case "products":
            await MigrateProductsAsync(RequireConnectionString(configuration, "products-db"));
            break;

        case "orders":
            await MigrateOrdersAsync(RequireConnectionString(configuration, "orders-db"));
            break;

        case "inventory":
            await MigrateInventoryAsync(RequireConnectionString(configuration, "inventory-db"));
            break;

        case "payments":
            await MigratePaymentsAsync(RequireConnectionString(configuration, "payments-db"));
            break;

        default:
            throw new InvalidOperationException(
                $"Unsupported MIGRATION_TARGET '{target}'. Expected customers, products, orders, inventory or payments.");
    }

    Console.WriteLine("Database migrations completed for {0}.", target);
}

static string RequireConnectionString(
    IConfiguration configuration,
    string name) =>
    configuration.GetConnectionString(name)
    ?? throw new InvalidOperationException(
        $"Connection string '{name}' was not provided by the migration secret boundary.");

static async Task MigrateProductsAsync(string connectionString)
{
    var options = new DbContextOptionsBuilder<ProductsDbContext>()
        .UseNpgsql(connectionString)
        .Options;

    await using var context = new ProductsDbContext(options);
    await context.Database.MigrateAsync();
}

static async Task MigrateCustomersAsync(string connectionString)
{
    var options = new DbContextOptionsBuilder<CustomersDbContext>()
        .UseNpgsql(connectionString)
        .Options;

    await using var context = new CustomersDbContext(options);
    await context.Database.MigrateAsync();
}

static async Task MigrateOrdersAsync(string connectionString)
{
    var options = new DbContextOptionsBuilder<OrdersDbContext>()
        .UseNpgsql(connectionString)
        .Options;

    await using var context = new OrdersDbContext(options);
    await context.Database.MigrateAsync();
}

static async Task MigrateInventoryAsync(string connectionString)
{
    var options = new DbContextOptionsBuilder<InventoryDbContext>()
        .UseNpgsql(connectionString)
        .Options;

    await using var context = new InventoryDbContext(options);
    await context.Database.MigrateAsync();
}

static async Task MigratePaymentsAsync(string connectionString)
{
    var options = new DbContextOptionsBuilder<PaymentsDbContext>()
        .UseNpgsql(connectionString)
        .Options;

    await using var context = new PaymentsDbContext(options);
    await context.Database.MigrateAsync();
}
