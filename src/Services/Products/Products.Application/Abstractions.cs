using Products.Domain;

namespace Products.Application;

public interface IProductRepository
{
    Task AddAsync(Product product, CancellationToken cancellationToken);
    Task<Product?> GetAsync(Guid id, bool tracked, CancellationToken cancellationToken);
    Task<bool> IdentifierExistsAsync(string sku, string? barcode, Guid? excludeId, CancellationToken cancellationToken);
    Task<(IReadOnlyList<Product> Items, int TotalCount)> SearchAsync(ProductQuery query, CancellationToken cancellationToken);
    void Remove(Product product);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
