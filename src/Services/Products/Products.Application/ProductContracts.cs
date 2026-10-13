namespace Products.Application;

public sealed record ProductInput(
    string Sku, string Name, string? Description, string Category,
    string? Brand, string? Barcode, string Unit, decimal Price);

public sealed record UpdateProductInput(
    string Sku, string Name, string? Description, string Category,
    string? Brand, string? Barcode, string Unit, decimal Price, bool IsActive);

public sealed record ProductQuery(string? Search, string? Category, bool? IsActive, int Page, int PageSize);

public sealed record ProductView(
    Guid Id, string Sku, string Name, string? Description, string Category,
    string? Brand, string? Barcode, string Unit, decimal Price, string Currency,
    bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record ProductPage(
    IReadOnlyList<ProductView> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public sealed class DuplicateProductIdentifierException : Exception
{
    public DuplicateProductIdentifierException() : base("A product with this SKU or barcode already exists.") { }
}

public sealed class ProductNotFoundException(Guid id)
    : Exception($"Product '{id}' does not exist.");
