using Products.Domain;

namespace Products.Domain.Tests;

[TestFixture]
public sealed class ProductTests
{
    [Test]
    public void Create_normalizes_sku_and_unit()
    {
        var product = Product.Create(" sku-001 ", " Notebook ", "Demo product", "Computers",
            "Example", "7891234567895", "un", 3999.90m);
        Assert.Multiple(() =>
        {
            Assert.That(product.Sku, Is.EqualTo("SKU-001"));
            Assert.That(product.Name, Is.EqualTo("Notebook"));
            Assert.That(product.Unit, Is.EqualTo("UN"));
            Assert.That(product.Currency, Is.EqualTo("BRL"));
            Assert.That(product.IsActive, Is.True);
        });
    }

    [TestCase("bad sku")]
    [TestCase("x")]
    [TestCase("éxí")]
    public void Invalid_sku_is_rejected(string sku) =>
        Assert.Throws<ArgumentException>(() => Product.Create(sku, "Product", null,
            "Category", null, null, "UN", 1.00m));

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(1.123)]
    public void Invalid_price_is_rejected(decimal price) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Product.Create("SKU-001", "Product",
            null, "Category", null, null, "UN", price));

    [Test]
    public void Update_changes_fields_and_inactive_status()
    {
        var product = Product.Create("SKU-001", "Original", null, "Category", null,
            null, "UN", 10m);
        product.Update("sku-001", "Updated", "Details", "Category", null, null,
            "UN", 20.99m, false);
        Assert.Multiple(() =>
        {
            Assert.That(product.Name, Is.EqualTo("Updated"));
            Assert.That(product.IsActive, Is.False);
            Assert.That(product.Price, Is.EqualTo(20.99m));
        });
    }

    [Test]
    public void Invalid_barcode_is_rejected() =>
        Assert.Throws<ArgumentException>(() => Product.Create("SKU-001", "Product",
            null, "Category", null, "abc", "UN", 1m));
}
