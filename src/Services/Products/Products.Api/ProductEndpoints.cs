using DistributedCommerce.Security;
using Products.Application;

namespace Products.Api;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var products = endpoints.MapGroup("/products");
        products.MapPost("", CreateAsync).RequireAuthorization(SecurityPolicies.ProductsWrite);
        products.MapGet("", SearchAsync).RequireAuthorization(SecurityPolicies.ProductsRead);
        products.MapGet("/{id:guid}", GetAsync).RequireAuthorization(SecurityPolicies.ProductsRead);
        products.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization(SecurityPolicies.ProductsWrite);
        products.MapDelete("/{id:guid}", DeleteAsync).RequireAuthorization(SecurityPolicies.ProductsWrite);
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(ProductInput request, ProductService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var product = await service.CreateAsync(request, cancellationToken);
            return Results.Created($"/products/{product.Id}", product);
        }
        catch (DuplicateProductIdentifierException)
        {
            return Conflict();
        }
        catch (Exception exception) when (IsValidation(exception))
        {
            return Invalid(exception);
        }
    }

    private static async Task<IResult> GetAsync(Guid id, ProductService service,
        CancellationToken cancellationToken)
    {
        var product = await service.GetAsync(id, cancellationToken);
        return product is null ? Results.NotFound() : Results.Ok(product);
    }

    private static async Task<IResult> SearchAsync(string? search, string? category,
        bool? isActive, int? page, int? pageSize, ProductService service,
        CancellationToken cancellationToken)
    {
        var result = await service.SearchAsync(
            new ProductQuery(search, category, isActive, page ?? 1, pageSize ?? 20),
            cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> UpdateAsync(Guid id, UpdateProductInput request,
        ProductService service, CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.UpdateAsync(id, request, cancellationToken));
        }
        catch (ProductNotFoundException)
        {
            return Results.NotFound();
        }
        catch (DuplicateProductIdentifierException)
        {
            return Conflict();
        }
        catch (Exception exception) when (IsValidation(exception))
        {
            return Invalid(exception);
        }
    }

    private static async Task<IResult> DeleteAsync(Guid id, ProductService service,
        CancellationToken cancellationToken) =>
        await service.DeleteAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound();

    private static bool IsValidation(Exception exception) =>
        exception is ArgumentException or InvalidOperationException;

    private static IResult Invalid(Exception exception) =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["request"] = [exception.Message]
        });

    private static IResult Conflict() =>
        Results.Conflict(new { message = "A product with this SKU or barcode already exists." });
}
