using Microsoft.EntityFrameworkCore;
using Products.Application;
using Products.Domain;
using Products.Infrastructure;
using Testcontainers.PostgreSql;

namespace Products.Infrastructure.Tests;

[TestFixture]
public sealed class ProductPersistenceTests
{
    private PostgreSqlContainer _postgres = null!;

    [OneTimeSetUp]
    public async Task StartAsync()
    {
        _postgres = new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase("products_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
        await _postgres.StartAsync();
    }

    [OneTimeTearDown]
    public async Task StopAsync() => await _postgres.DisposeAsync();

    [Test]
    public async Task Product_crud_search_and_unique_sku_work_with_real_migrations()
    {
        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseNpgsql(_postgres.GetConnectionString()).Options;

        var created = Product.Create("SKU-001", "Notebook", "Demo", "Computers", null,
            null, "UN", 2000m);

        await using (var db = new ProductsDbContext(options))
        {
            await db.Database.MigrateAsync();
            await db.Products.AddAsync(created);
            await db.SaveChangesAsync();
        }

        await using (var db = new ProductsDbContext(options))
        {
            var repository = new ProductRepository(db);
            var (items, total) = await repository.SearchAsync(
                new ProductQuery("note", "Computers", true, 1, 20),
                CancellationToken.None);
            Assert.That(total, Is.EqualTo(1));
            Assert.That(items.Single().Sku, Is.EqualTo("SKU-001"));

            var tracked = await repository.GetAsync(created.Id, true, CancellationToken.None);
            Assert.That(tracked, Is.Not.Null);
            tracked!.Update("SKU-001", "Updated", null, "Computers", null, null, "UN", 1000m, false);
            await db.SaveChangesAsync();
        }

        await using (var db = new ProductsDbContext(options))
        {
            var updated = await db.Products.AsNoTracking().SingleAsync();
            Assert.That(updated.Name, Is.EqualTo("Updated"));
            Assert.That(updated.IsActive, Is.False);

            await db.Products.AddAsync(Product.Create("sku-001", "Duplicate", null,
                "Computers", null, null, "UN", 99m));
            Assert.ThrowsAsync<DuplicateProductIdentifierException>(
                async () => await db.SaveChangesAsync());
        }

        await using (var db = new ProductsDbContext(options))
        {
            var product = await db.Products.SingleAsync();
            db.Products.Remove(product);
            await db.SaveChangesAsync();
            Assert.That(await db.Products.CountAsync(), Is.Zero);
        }
    }
}
