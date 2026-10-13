using Products.Domain;

namespace Products.Application;

public sealed class ProductService(IProductRepository repository, IUnitOfWork unitOfWork)
{
    public async Task<ProductView> CreateAsync(ProductInput request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var product = Product.Create(request.Sku, request.Name, request.Description,
            request.Category, request.Brand, request.Barcode, request.Unit, request.Price);

        if (await repository.IdentifierExistsAsync(product.Sku, product.Barcode, null, cancellationToken))
            throw new DuplicateProductIdentifierException();

        await repository.AddAsync(product, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(product);
    }

    public async Task<ProductView?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await repository.GetAsync(id, false, cancellationToken);
        return product is null ? null : Map(product);
    }

    public async Task<ProductPage> SearchAsync(ProductQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var page = Math.Max(query.Page, 1);
        var pageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, 100);
        var normalized = query with
        {
            Page = page,
            PageSize = pageSize,
            Search = query.Search?.Trim(),
            Category = query.Category?.Trim()
        };
        var (items, total) = await repository.SearchAsync(normalized, cancellationToken);
        return new ProductPage(items.Select(Map).ToArray(), page, pageSize, total,
            total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<ProductView> UpdateAsync(Guid id, UpdateProductInput request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var product = await repository.GetAsync(id, true, cancellationToken)
            ?? throw new ProductNotFoundException(id);
        var normalizedSku = Product.NormalizeSku(request.Sku);
        var normalizedBarcode = Product.NormalizeBarcode(request.Barcode);
        if (await repository.IdentifierExistsAsync(normalizedSku, normalizedBarcode, id, cancellationToken))
            throw new DuplicateProductIdentifierException();

        product.Update(normalizedSku, request.Name, request.Description, request.Category,
            request.Brand, normalizedBarcode, request.Unit, request.Price, request.IsActive);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(product);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await repository.GetAsync(id, true, cancellationToken);
        if (product is null) return false;
        repository.Remove(product);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static ProductView Map(Product product) =>
        new(product.Id, product.Sku, product.Name, product.Description, product.Category,
            product.Brand, product.Barcode, product.Unit, product.Price, product.Currency,
            product.IsActive, product.CreatedAt, product.UpdatedAt);
}
