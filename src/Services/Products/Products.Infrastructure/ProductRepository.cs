using Microsoft.EntityFrameworkCore;
using Products.Application;
using Products.Domain;

namespace Products.Infrastructure;

public sealed class ProductRepository(ProductsDbContext db) : IProductRepository
{
    public async Task AddAsync(Product product, CancellationToken cancellationToken) =>
        await db.Products.AddAsync(product, cancellationToken);

    public Task<Product?> GetAsync(Guid id, bool tracked, CancellationToken cancellationToken)
    {
        var query = tracked ? db.Products : db.Products.AsNoTracking();
        return query.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public Task<bool> IdentifierExistsAsync(string sku, string? barcode, Guid? excludeId,
        CancellationToken cancellationToken) =>
        db.Products.AnyAsync(p =>
            (!excludeId.HasValue || p.Id != excludeId.Value) &&
            (p.Sku == sku || (barcode != null && p.Barcode == barcode)), cancellationToken);

    public async Task<(IReadOnlyList<Product> Items, int TotalCount)> SearchAsync(
        ProductQuery query, CancellationToken cancellationToken)
    {
        IQueryable<Product> products = db.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var text = "%" + query.Search + "%";
            products = products.Where(p => EF.Functions.ILike(p.Name, text)
                || EF.Functions.ILike(p.Sku, text)
                || (p.Description != null && EF.Functions.ILike(p.Description, text)));
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
            products = products.Where(p => EF.Functions.ILike(p.Category, query.Category));

        if (query.IsActive.HasValue)
            products = products.Where(p => p.IsActive == query.IsActive.Value);

        var total = await products.CountAsync(cancellationToken);
        var items = await products
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public void Remove(Product product) => db.Products.Remove(product);
}
