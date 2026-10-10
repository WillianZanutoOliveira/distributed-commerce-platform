using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace DistributedCommerce.Security;

public static class SecurityPolicies
{
    public const string OrdersRead = "orders.read";
    public const string OrdersWrite = "orders.write";
    public const string CustomersRead = "customers.read";
    public const string CustomersWrite = "customers.write";
    public const string ProductsRead = "products.read";
    public const string ProductsWrite = "products.write";
    public const string PlatformAdmin = "platform.admin";
}

public static class KeycloakAuthenticationExtensions
{
    public static IServiceCollection AddPlatformIdentity(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var keycloak = KeycloakOptions.From(configuration);
        ValidateTransportSecurity(keycloak, environment);

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = keycloak.Authority;
                options.RequireHttpsMetadata = keycloak.RequireHttpsMetadata;
                options.MapInboundClaims = false;
                options.IncludeErrorDetails = false;
                options.SaveToken = false;

                if (keycloak.MetadataAddress is not null)
                    options.MetadataAddress = keycloak.MetadataAddress;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    RequireSignedTokens = true,
                    ValidateIssuer = true,
                    ValidIssuer = keycloak.Issuer,
                    ValidateAudience = true,
                    ValidAudience = keycloak.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    NameClaimType = "preferred_username",
                    RoleClaimType = "roles",
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        services
            .AddAuthorizationBuilder()
            .AddPolicy(
                SecurityPolicies.OrdersRead,
                policy => policy.RequireAuthenticatedUser().RequireRole("customer", "admin"))
            .AddPolicy(
                SecurityPolicies.OrdersWrite,
                policy => policy.RequireAuthenticatedUser().RequireRole("customer", "admin"))
            .AddPolicy(
                SecurityPolicies.CustomersRead,
                policy => policy.RequireAuthenticatedUser().RequireRole("admin"))
            .AddPolicy(
                SecurityPolicies.CustomersWrite,
                policy => policy.RequireAuthenticatedUser().RequireRole("admin"))
            .AddPolicy(
                SecurityPolicies.ProductsRead,
                policy => policy.RequireAuthenticatedUser().RequireRole("customer", "admin"))
            .AddPolicy(
                SecurityPolicies.ProductsWrite,
                policy => policy.RequireAuthenticatedUser().RequireRole("admin"))
            .AddPolicy(
                SecurityPolicies.PlatformAdmin,
                policy => policy.RequireAuthenticatedUser().RequireRole("admin"));

        return services;
    }

    private static void ValidateTransportSecurity(
        KeycloakOptions keycloak,
        IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
            return;

        if (!keycloak.RequireHttpsMetadata)
        {
            throw new InvalidOperationException(
                "Keycloak HTTPS metadata validation cannot be disabled outside Development.");
        }

        EnsureHttps("Keycloak:Authority", keycloak.Authority);
        EnsureHttps("Keycloak:Issuer", keycloak.Issuer);

        if (keycloak.MetadataAddress is not null)
            EnsureHttps("Keycloak:MetadataAddress", keycloak.MetadataAddress);
    }

    private static void EnsureHttps(string key, string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Configuration '{key}' must use HTTPS outside Development.");
        }
    }
}
