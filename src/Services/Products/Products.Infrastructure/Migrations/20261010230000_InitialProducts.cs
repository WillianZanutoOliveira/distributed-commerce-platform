using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Products.Infrastructure.Migrations;

[DbContext(typeof(ProductsDbContext))]
[Migration("20261010230000_InitialProducts")]
public sealed class InitialProducts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "products",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Brand = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Barcode = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                Unit = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_products", x => x.Id));

        migrationBuilder.CreateIndex("IX_products_Sku", "products", "Sku", unique: true);
        migrationBuilder.CreateIndex("IX_products_Barcode", "products", "Barcode", unique: true);
        migrationBuilder.CreateIndex("IX_products_Name", "products", "Name");
        migrationBuilder.CreateIndex("IX_products_Category", "products", "Category");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "products");
}
