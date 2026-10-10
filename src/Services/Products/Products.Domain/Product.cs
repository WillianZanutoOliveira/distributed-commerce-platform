namespace Products.Domain;

public sealed class Product
{
    private Product() { }

    private Product(string sku, string name, string? description, string category,
        string? brand, string? barcode, string unit, decimal price)
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
        IsActive = true;
        Apply(sku, name, description, category, brand, barcode, unit, price);
    }

    public Guid Id { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public string? Brand { get; private set; }
    public string? Barcode { get; private set; }
    public string Unit { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public string Currency { get; private set; } = "BRL";
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Product Create(string sku, string name, string? description,
        string category, string? brand, string? barcode, string unit, decimal price) =>
        new(sku, name, description, category, brand, barcode, unit, price);

    public void Update(string sku, string name, string? description, string category,
        string? brand, string? barcode, string unit, decimal price, bool isActive)
    {
        Apply(sku, name, description, category, brand, barcode, unit, price);
        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public static string NormalizeSku(string sku)
    {
        var result = Required(sku, nameof(sku), 64).ToUpperInvariant();
        if (result.Length < 3 ||
            !char.IsAsciiLetterOrDigit(result[0]) ||
            result.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_')))
            throw new ArgumentException("SKU must be 3-64 ASCII letters, digits, '-' or '_', beginning with a letter or digit.", nameof(sku));

        return result;
    }

    public static string? NormalizeBarcode(string? barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return null;

        var result = barcode.Trim();
        if (result.Length is not (8 or 12 or 13 or 14) || result.Any(c => !char.IsAsciiDigit(c)))
            throw new ArgumentException("Barcode must contain 8, 12, 13 or 14 digits.", nameof(barcode));

        return result;
    }

    private void Apply(string sku, string name, string? description, string category,
        string? brand, string? barcode, string unit, decimal price)
    {
        if (price <= 0 || price > 9999999999999999.99m || decimal.Round(price, 2) != price)
            throw new ArgumentOutOfRangeException(nameof(price), "Price must be positive with at most two decimal places.");

        var normalizedUnit = Required(unit, nameof(unit), 10).ToUpperInvariant();
        if (normalizedUnit.Any(c => !char.IsAsciiLetter(c)))
            throw new ArgumentException("Unit must contain ASCII letters only.", nameof(unit));

        Sku = NormalizeSku(sku);
        Name = Required(name, nameof(name), 200);
        Description = Optional(description, 2000);
        Category = Required(category, nameof(category), 100);
        Brand = Optional(brand, 100);
        Barcode = NormalizeBarcode(barcode);
        Unit = normalizedUnit;
        Price = price;
    }

    private static string Required(string? value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value is required.", name);

        var result = value.Trim();
        if (result.Length > maxLength)
            throw new ArgumentException($"Value exceeds {maxLength} characters.", name);
        return result;
    }

    private static string? Optional(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? null : Required(value, nameof(value), maxLength);
}
