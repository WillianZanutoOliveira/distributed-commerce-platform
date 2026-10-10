using System.Text.Json;
using System.Text.Json.Serialization;
using DistributedCommerce.Secrets;
using DistributedCommerce.Security;
using DistributedCommerce.ServiceDefaults;
using Products.Api;
using Products.Application;
using Products.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
await builder.Configuration.AddVaultSecretsAsync();
builder.Services.AddVaultLeaseRenewal();

var connectionString = builder.Configuration.GetConnectionString("products-db")
    ?? throw new InvalidOperationException(
        "Products database connection must be supplied through the configured secret boundary.");

builder.Services.AddProductsInfrastructure(connectionString);
builder.Services.AddScoped<ProductService>();
builder.AddPlatformServiceDefaults("products-api");
builder.AddPlatformWebSecurity();
builder.Services.AddPlatformIdentity(builder.Configuration, builder.Environment);
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.MaxDepth = 16;
    options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

var app = builder.Build();
app.UseExceptionHandler(new Microsoft.AspNetCore.Builder.ExceptionHandlerOptions
{
    StatusCodeSelector = exception =>
        exception is BadHttpRequestException badRequest ? badRequest.StatusCode
            : StatusCodes.Status500InternalServerError
});
app.UsePlatformWebSecurity();
app.UseAuthentication();
app.UseAuthorization();
app.MapPlatformDefaultEndpoints();
if (app.Environment.IsDevelopment())
    app.MapOpenApi();
app.MapProductEndpoints();
await app.RunAsync();
