using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);

var repositoryRoot = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "../../.."));
var vaultScript = Path.Combine(repositoryRoot, "deploy", "vault", "vault-init.sh");
var keycloakRealmDirectory = Path.Combine(repositoryRoot, "deploy", "keycloak");
var tokenRoot = Path.Combine(repositoryRoot, ".aspire", "vault-tokens");

var tokenDirectories = new Dictionary<string, string>(StringComparer.Ordinal)
{
    ["orders"] = Path.Combine(tokenRoot, "orders"),
    ["inventory"] = Path.Combine(tokenRoot, "inventory"),
    ["payments"] = Path.Combine(tokenRoot, "payments"),
    ["customers"] = Path.Combine(tokenRoot, "customers"),
    ["products"] = Path.Combine(tokenRoot, "products"),
    ["notifications"] = Path.Combine(tokenRoot, "notifications"),
    ["orders-migrator"] = Path.Combine(tokenRoot, "orders-migrator"),
    ["inventory-migrator"] = Path.Combine(tokenRoot, "inventory-migrator"),
    ["payments-migrator"] = Path.Combine(tokenRoot, "payments-migrator"),
    ["customers-migrator"] = Path.Combine(tokenRoot, "customers-migrator"),
    ["products-migrator"] = Path.Combine(tokenRoot, "products-migrator")
};

foreach (var directory in tokenDirectories.Values)
{
    Directory.CreateDirectory(directory);

    if (!OperatingSystem.IsWindows())
    {
        File.SetUnixFileMode(
            directory,
            UnixFileMode.UserRead |
            UnixFileMode.UserWrite |
            UnixFileMode.UserExecute);
    }
}

var vaultRootToken = CreateGeneratedSecret(builder, "vault-dev-root-token");
var ordersDbPassword = CreateGeneratedSecret(builder, "orders-db-password");
var inventoryDbPassword = CreateGeneratedSecret(builder, "inventory-db-password");
var paymentsDbPassword = CreateGeneratedSecret(builder, "payments-db-password");
var customersDbPassword = CreateGeneratedSecret(builder, "customers-db-password");
var productsDbPassword = CreateGeneratedSecret(builder, "products-db-password");
var rabbitMqPassword = CreateGeneratedSecret(builder, "rabbitmq-password");
var keycloakAdminPassword = CreateGeneratedSecret(builder, "keycloak-admin-password");

var ordersDb = AddPostgres(
    builder,
    "orders-db",
    "orders",
    ordersDbPassword,
    port: 5432,
    Path.Combine(repositoryRoot, "deploy", "postgres", "orders-init.sql"));

var inventoryDb = AddPostgres(
    builder,
    "inventory-db",
    "inventory",
    inventoryDbPassword,
    port: 5433,
    Path.Combine(repositoryRoot, "deploy", "postgres", "inventory-init.sql"));

var paymentsDb = AddPostgres(
    builder,
    "payments-db",
    "payments",
    paymentsDbPassword,
    port: 5434,
    Path.Combine(repositoryRoot, "deploy", "postgres", "payments-init.sql"));

var customersDb = AddPostgres(
    builder,
    "customers-db",
    "customers",
    customersDbPassword,
    port: 5435,
    Path.Combine(repositoryRoot, "deploy", "postgres", "customers-init.sql"));

var productsDb = AddPostgres(
    builder,
    "products-db",
    "products",
    productsDbPassword,
    port: 5436,
    Path.Combine(repositoryRoot, "deploy", "postgres", "products-init.sql"));

var rabbitMq = builder
    .AddContainer("rabbitmq", "rabbitmq", "4-management")
    .WithEnvironment("RABBITMQ_DEFAULT_USER", "platform")
    .WithEnvironment("RABBITMQ_DEFAULT_PASS", rabbitMqPassword)
    .WithEndpoint(port: 5672, targetPort: 5672, name: "amqp", isProxied: false)
    .WithHttpEndpoint(port: 15672, targetPort: 15672, name: "management", isProxied: false);

var keycloak = builder
    .AddContainer("keycloak", "quay.io/keycloak/keycloak", "26.8.0")
    .WithArgs("start-dev", "--import-realm")
    .WithEnvironment("KC_BOOTSTRAP_ADMIN_USERNAME", "admin")
    .WithEnvironment("KC_BOOTSTRAP_ADMIN_PASSWORD", keycloakAdminPassword)
    .WithBindMount(
        keycloakRealmDirectory,
        "/opt/keycloak/data/import",
        isReadOnly: true)
    .WithHttpEndpoint(port: 8180, targetPort: 8080, name: "http", isProxied: false)
    .WithHttpHealthCheck("/realms/distributed-commerce/.well-known/openid-configuration")
    .WithOtlpExporter();

var vault = builder
    .AddContainer("vault", "hashicorp/vault", "1.21.4")
    .WithArgs("server", "-dev")
    .WithEnvironment("VAULT_DEV_ROOT_TOKEN_ID", vaultRootToken)
    .WithEnvironment("VAULT_DEV_LISTEN_ADDRESS", "0.0.0.0:8200")
    .WithHttpEndpoint(port: 8200, targetPort: 8200, name: "http", isProxied: false)
    .WithHttpHealthCheck("/v1/sys/health");

var vaultInit = builder
    .AddContainer("vault-init", "hashicorp/vault", "1.21.4")
    .WithEntrypoint("/bin/sh")
    .WithArgs("/bootstrap/vault-init.sh")
#pragma warning disable S5332 // Vault server-dev is intentionally HTTP-only inside the isolated local network.
    .WithEnvironment("VAULT_ADDR", "http://vault:8200")
#pragma warning restore S5332
    .WithEnvironment("VAULT_DEV_ROOT_TOKEN_ID", vaultRootToken)
    .WithEnvironment("VAULT_TOKEN_FILE_MODE", "0444")
    .WithEnvironment("ORDERS_POSTGRES_USER", "postgres")
    .WithEnvironment("ORDERS_POSTGRES_PASSWORD", ordersDbPassword)
    .WithEnvironment("INVENTORY_POSTGRES_USER", "postgres")
    .WithEnvironment("INVENTORY_POSTGRES_PASSWORD", inventoryDbPassword)
    .WithEnvironment("PAYMENTS_POSTGRES_USER", "postgres")
    .WithEnvironment("PAYMENTS_POSTGRES_PASSWORD", paymentsDbPassword)
    .WithEnvironment("CUSTOMERS_POSTGRES_USER", "postgres")
    .WithEnvironment("CUSTOMERS_POSTGRES_PASSWORD", customersDbPassword)
    .WithEnvironment("PRODUCTS_POSTGRES_USER", "postgres")
    .WithEnvironment("PRODUCTS_POSTGRES_PASSWORD", productsDbPassword)
    .WithEnvironment("RABBITMQ_DEFAULT_USER", "platform")
    .WithEnvironment("RABBITMQ_DEFAULT_PASS", rabbitMqPassword)
    .WithBindMount(vaultScript, "/bootstrap/vault-init.sh", isReadOnly: true)
    .WithBindMount(tokenDirectories["orders"], "/tokens/orders")
    .WithBindMount(tokenDirectories["inventory"], "/tokens/inventory")
    .WithBindMount(tokenDirectories["payments"], "/tokens/payments")
    .WithBindMount(tokenDirectories["notifications"], "/tokens/notifications")
    .WithBindMount(tokenDirectories["orders-migrator"], "/tokens/orders-migrator")
    .WithBindMount(tokenDirectories["inventory-migrator"], "/tokens/inventory-migrator")
    .WithBindMount(tokenDirectories["payments-migrator"], "/tokens/payments-migrator")
    .WithBindMount(tokenDirectories["customers"], "/tokens/customers")
    .WithBindMount(tokenDirectories["customers-migrator"], "/tokens/customers-migrator")
    .WithBindMount(tokenDirectories["products"], "/tokens/products")
    .WithBindMount(tokenDirectories["products-migrator"], "/tokens/products-migrator")
    .WaitFor(vault)
    .WaitFor(ordersDb)
    .WaitFor(inventoryDb)
    .WaitFor(paymentsDb)
    .WaitFor(customersDb)
    .WaitFor(productsDb);

var databaseMigratorProject = Path.Combine(
    repositoryRoot,
    "src",
    "Platform",
    "DatabaseMigrator",
    "DatabaseMigrator.csproj");

var ordersMigrator = builder
    .AddProject("orders-migrator", databaseMigratorProject)
    .WithEnvironment("MIGRATION_TARGET", "orders")
    .WithEnvironment("ConnectionStrings__orders-db", "")
    .WithEnvironment("Vault__Address", "http://localhost:8200")
    .WithEnvironment("Vault__TokenFile", Path.Combine(tokenDirectories["orders-migrator"], "token"))
    .WithEnvironment("Vault__DatabaseRole", "orders-migration")
    .WithEnvironment("Vault__DatabaseConnectionStringName", "orders-db")
    .WithEnvironment("Vault__DatabaseHost", "localhost")
    .WithEnvironment("Vault__DatabasePort", "5432")
    .WithEnvironment("Vault__DatabaseName", "orders")
    .WithEnvironment("Vault__DatabaseRuntimeRole", "orders_migrator")
    .WaitForCompletion(vaultInit);

var inventoryMigrator = builder
    .AddProject("inventory-migrator", databaseMigratorProject)
    .WithEnvironment("MIGRATION_TARGET", "inventory")
    .WithEnvironment("ConnectionStrings__inventory-db", "")
    .WithEnvironment("Vault__Address", "http://localhost:8200")
    .WithEnvironment("Vault__TokenFile", Path.Combine(tokenDirectories["inventory-migrator"], "token"))
    .WithEnvironment("Vault__DatabaseRole", "inventory-migration")
    .WithEnvironment("Vault__DatabaseConnectionStringName", "inventory-db")
    .WithEnvironment("Vault__DatabaseHost", "localhost")
    .WithEnvironment("Vault__DatabasePort", "5433")
    .WithEnvironment("Vault__DatabaseName", "inventory")
    .WithEnvironment("Vault__DatabaseRuntimeRole", "inventory_migrator")
    .WaitForCompletion(vaultInit);

var paymentsMigrator = builder
    .AddProject("payments-migrator", databaseMigratorProject)
    .WithEnvironment("MIGRATION_TARGET", "payments")
    .WithEnvironment("ConnectionStrings__payments-db", "")
    .WithEnvironment("Vault__Address", "http://localhost:8200")
    .WithEnvironment("Vault__TokenFile", Path.Combine(tokenDirectories["payments-migrator"], "token"))
    .WithEnvironment("Vault__DatabaseRole", "payments-migration")
    .WithEnvironment("Vault__DatabaseConnectionStringName", "payments-db")
    .WithEnvironment("Vault__DatabaseHost", "localhost")
    .WithEnvironment("Vault__DatabasePort", "5434")
    .WithEnvironment("Vault__DatabaseName", "payments")
    .WithEnvironment("Vault__DatabaseRuntimeRole", "payments_migrator")
    .WaitForCompletion(vaultInit);

var customersMigrator = builder
    .AddProject("customers-migrator", databaseMigratorProject)
    .WithEnvironment("MIGRATION_TARGET", "customers")
    .WithEnvironment("ConnectionStrings__customers-db", "")
    .WithEnvironment("Vault__Address", "http://localhost:8200")
    .WithEnvironment("Vault__TokenFile", Path.Combine(tokenDirectories["customers-migrator"], "token"))
    .WithEnvironment("Vault__DatabaseRole", "customers-migration")
    .WithEnvironment("Vault__DatabaseConnectionStringName", "customers-db")
    .WithEnvironment("Vault__DatabaseHost", "localhost")
    .WithEnvironment("Vault__DatabasePort", "5435")
    .WithEnvironment("Vault__DatabaseName", "customers")
    .WithEnvironment("Vault__DatabaseRuntimeRole", "customers_migrator")
    .WaitForCompletion(vaultInit);

var productsMigrator = builder
    .AddProject("products-migrator", databaseMigratorProject)
    .WithEnvironment("MIGRATION_TARGET", "products")
    .WithEnvironment("ConnectionStrings__products-db", "")
    .WithEnvironment("Vault__Address", "http://localhost:8200")
    .WithEnvironment("Vault__TokenFile", Path.Combine(tokenDirectories["products-migrator"], "token"))
    .WithEnvironment("Vault__DatabaseRole", "products-migration")
    .WithEnvironment("Vault__DatabaseConnectionStringName", "products-db")
    .WithEnvironment("Vault__DatabaseHost", "localhost")
    .WithEnvironment("Vault__DatabasePort", "5436")
    .WithEnvironment("Vault__DatabaseName", "products")
    .WithEnvironment("Vault__DatabaseRuntimeRole", "products_migrator")
    .WaitForCompletion(vaultInit);

var ordersApi = builder
    .AddProject(
        "orders-api",
        Path.Combine(repositoryRoot, "src", "Services", "Orders", "Orders.Api", "Orders.Api.csproj"))
    .WithHttpEndpoint(port: 8081, targetPort: 8081, name: "http", env: "ASPNETCORE_HTTP_PORTS", isProxied: false)
    .WithHttpHealthCheck("/health")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("RabbitMq__Host", "localhost")
    .WithEnvironment("RabbitMq__Username", "")
    .WithEnvironment("RabbitMq__Password", "")
    .WithEnvironment("Keycloak__Authority", "http://localhost:8180/realms/distributed-commerce")
    .WithEnvironment("Keycloak__MetadataAddress", "http://localhost:8180/realms/distributed-commerce/.well-known/openid-configuration")
    .WithEnvironment("Keycloak__Issuer", "http://localhost:8180/realms/distributed-commerce")
    .WithEnvironment("Keycloak__Audience", "distributed-commerce-api")
    .WithEnvironment("Keycloak__RequireHttpsMetadata", "false")
    .WithEnvironment("ConnectionStrings__orders-db", "")
    .WithEnvironment("Vault__Address", "http://localhost:8200")
    .WithEnvironment("Vault__TokenFile", Path.Combine(tokenDirectories["orders"], "token"))
    .WithEnvironment("Vault__SecretPath", "platform/orders")
    .WithEnvironment("Vault__DatabaseRole", "orders-app")
    .WithEnvironment("Vault__DatabaseConnectionStringName", "orders-db")
    .WithEnvironment("Vault__DatabaseHost", "localhost")
    .WithEnvironment("Vault__DatabasePort", "5432")
    .WithEnvironment("Vault__DatabaseName", "orders")
    .WithEnvironment("Vault__DatabaseRuntimeRole", "orders_runtime")
    .WithOtlpExporter()
    .WaitForCompletion(vaultInit)
    .WaitForCompletion(ordersMigrator)
    .WaitFor(rabbitMq)
    .WaitFor(keycloak);

var customersApi = builder
    .AddProject(
        "customers-api",
        Path.Combine(repositoryRoot, "src", "Services", "Customers", "Customers.Api", "Customers.Api.csproj"))
    .WithHttpEndpoint(port: 8085, targetPort: 8085, name: "http", env: "ASPNETCORE_HTTP_PORTS", isProxied: false)
    .WithHttpHealthCheck("/health")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("Keycloak__Authority", "http://localhost:8180/realms/distributed-commerce")
    .WithEnvironment("Keycloak__MetadataAddress", "http://localhost:8180/realms/distributed-commerce/.well-known/openid-configuration")
    .WithEnvironment("Keycloak__Issuer", "http://localhost:8180/realms/distributed-commerce")
    .WithEnvironment("Keycloak__Audience", "distributed-commerce-api")
    .WithEnvironment("Keycloak__RequireHttpsMetadata", "false")
    .WithEnvironment("BrasilApi__BaseUrl", "https://brasilapi.com.br/")
    .WithEnvironment("ConnectionStrings__customers-db", "")
    .WithEnvironment("Vault__Address", "http://localhost:8200")
    .WithEnvironment("Vault__TokenFile", Path.Combine(tokenDirectories["customers"], "token"))
    .WithEnvironment("Vault__DatabaseRole", "customers-app")
    .WithEnvironment("Vault__DatabaseConnectionStringName", "customers-db")
    .WithEnvironment("Vault__DatabaseHost", "localhost")
    .WithEnvironment("Vault__DatabasePort", "5435")
    .WithEnvironment("Vault__DatabaseName", "customers")
    .WithEnvironment("Vault__DatabaseRuntimeRole", "customers_runtime")
    .WithOtlpExporter()
    .WaitForCompletion(vaultInit)
    .WaitForCompletion(customersMigrator)
    .WaitFor(keycloak);

var productsApi = builder
    .AddProject(
        "products-api",
        Path.Combine(repositoryRoot, "src", "Services", "Products", "Products.Api", "Products.Api.csproj"))
    .WithHttpEndpoint(port: 8086, targetPort: 8086, name: "http", env: "ASPNETCORE_HTTP_PORTS", isProxied: false)
    .WithHttpHealthCheck("/health")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("Keycloak__Authority", "http://localhost:8180/realms/distributed-commerce")
    .WithEnvironment("Keycloak__MetadataAddress", "http://localhost:8180/realms/distributed-commerce/.well-known/openid-configuration")
    .WithEnvironment("Keycloak__Issuer", "http://localhost:8180/realms/distributed-commerce")
    .WithEnvironment("Keycloak__Audience", "distributed-commerce-api")
    .WithEnvironment("Keycloak__RequireHttpsMetadata", "false")
    .WithEnvironment("ConnectionStrings__products-db", "")
    .WithEnvironment("Vault__Address", "http://localhost:8200")
    .WithEnvironment("Vault__TokenFile", Path.Combine(tokenDirectories["products"], "token"))
    .WithEnvironment("Vault__DatabaseRole", "products-app")
    .WithEnvironment("Vault__DatabaseConnectionStringName", "products-db")
    .WithEnvironment("Vault__DatabaseHost", "localhost")
    .WithEnvironment("Vault__DatabasePort", "5436")
    .WithEnvironment("Vault__DatabaseName", "products")
    .WithEnvironment("Vault__DatabaseRuntimeRole", "products_runtime")
    .WithOtlpExporter()
    .WaitForCompletion(vaultInit)
    .WaitForCompletion(productsMigrator)
    .WaitFor(keycloak);

builder
    .AddProject(
        "inventory-service",
        Path.Combine(repositoryRoot, "src", "Services", "Inventory", "Inventory.Service", "Inventory.Service.csproj"))
    .WithHttpEndpoint(port: 8082, targetPort: 8082, name: "http", env: "ASPNETCORE_HTTP_PORTS", isProxied: false)
    .WithHttpHealthCheck("/health")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("RabbitMq__Host", "localhost")
    .WithEnvironment("RabbitMq__Username", "")
    .WithEnvironment("RabbitMq__Password", "")
    .WithEnvironment("ConnectionStrings__inventory-db", "")
    .WithEnvironment("Vault__Address", "http://localhost:8200")
    .WithEnvironment("Vault__TokenFile", Path.Combine(tokenDirectories["inventory"], "token"))
    .WithEnvironment("Vault__SecretPath", "platform/inventory")
    .WithEnvironment("Vault__DatabaseRole", "inventory-app")
    .WithEnvironment("Vault__DatabaseConnectionStringName", "inventory-db")
    .WithEnvironment("Vault__DatabaseHost", "localhost")
    .WithEnvironment("Vault__DatabasePort", "5433")
    .WithEnvironment("Vault__DatabaseName", "inventory")
    .WithEnvironment("Vault__DatabaseRuntimeRole", "inventory_runtime")
    .WithOtlpExporter()
    .WaitForCompletion(vaultInit)
    .WaitForCompletion(inventoryMigrator)
    .WaitFor(rabbitMq);

builder
    .AddProject(
        "payments-service",
        Path.Combine(repositoryRoot, "src", "Services", "Payments", "Payments.Service", "Payments.Service.csproj"))
    .WithHttpEndpoint(port: 8083, targetPort: 8083, name: "http", env: "ASPNETCORE_HTTP_PORTS", isProxied: false)
    .WithHttpHealthCheck("/health")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("RabbitMq__Host", "localhost")
    .WithEnvironment("RabbitMq__Username", "")
    .WithEnvironment("RabbitMq__Password", "")
    .WithEnvironment("ConnectionStrings__payments-db", "")
    .WithEnvironment("Vault__Address", "http://localhost:8200")
    .WithEnvironment("Vault__TokenFile", Path.Combine(tokenDirectories["payments"], "token"))
    .WithEnvironment("Vault__SecretPath", "platform/payments")
    .WithEnvironment("Vault__DatabaseRole", "payments-app")
    .WithEnvironment("Vault__DatabaseConnectionStringName", "payments-db")
    .WithEnvironment("Vault__DatabaseHost", "localhost")
    .WithEnvironment("Vault__DatabasePort", "5434")
    .WithEnvironment("Vault__DatabaseName", "payments")
    .WithEnvironment("Vault__DatabaseRuntimeRole", "payments_runtime")
    .WithOtlpExporter()
    .WaitForCompletion(vaultInit)
    .WaitForCompletion(paymentsMigrator)
    .WaitFor(rabbitMq);

builder
    .AddProject(
        "notifications-service",
        Path.Combine(repositoryRoot, "src", "Services", "Notifications", "Notifications.Service", "Notifications.Service.csproj"))
    .WithHttpEndpoint(port: 8084, targetPort: 8084, name: "http", env: "ASPNETCORE_HTTP_PORTS", isProxied: false)
    .WithHttpHealthCheck("/health")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("RabbitMq__Host", "localhost")
    .WithEnvironment("RabbitMq__Username", "")
    .WithEnvironment("RabbitMq__Password", "")
    .WithEnvironment("Vault__Address", "http://localhost:8200")
    .WithEnvironment("Vault__TokenFile", Path.Combine(tokenDirectories["notifications"], "token"))
    .WithEnvironment("Vault__SecretPath", "platform/notifications")
    .WithOtlpExporter()
    .WaitForCompletion(vaultInit)
    .WaitFor(rabbitMq);

builder
    .AddProject(
        "api-gateway",
        Path.Combine(repositoryRoot, "src", "Gateway", "ApiGateway", "ApiGateway.csproj"))
    .WithHttpEndpoint(port: 8080, targetPort: 8080, name: "http", env: "ASPNETCORE_HTTP_PORTS", isProxied: false)
    .WithHttpHealthCheck("/health")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("Keycloak__Authority", "http://localhost:8180/realms/distributed-commerce")
    .WithEnvironment("Keycloak__MetadataAddress", "http://localhost:8180/realms/distributed-commerce/.well-known/openid-configuration")
    .WithEnvironment("Keycloak__Issuer", "http://localhost:8180/realms/distributed-commerce")
    .WithEnvironment("Keycloak__Audience", "distributed-commerce-api")
    .WithEnvironment("Keycloak__RequireHttpsMetadata", "false")
    .WithEnvironment("ReverseProxy__Clusters__orders-cluster__Destinations__primary__Address", "http://localhost:8081/")
    .WithEnvironment("ReverseProxy__Clusters__customers-cluster__Destinations__primary__Address", "http://localhost:8085/")
    .WithEnvironment("ReverseProxy__Clusters__products-cluster__Destinations__primary__Address", "http://localhost:8086/")
    .WithOtlpExporter()
    .WaitFor(keycloak)
    .WaitFor(ordersApi)
    .WaitFor(customersApi)
    .WaitFor(productsApi);

await builder.Build().RunAsync();

static IResourceBuilder<ParameterResource> CreateGeneratedSecret(
    IDistributedApplicationBuilder builder,
    string name)
{
    return builder.AddParameter(
        name,
        new GenerateParameterDefault
        {
            MinLength = 32,
            MinLower = 4,
            MinUpper = 4,
            MinNumeric = 4,
            Special = false
        },
        secret: true,
        persist: true);
}

static IResourceBuilder<ContainerResource> AddPostgres(
    IDistributedApplicationBuilder builder,
    string resourceName,
    string databaseName,
    IResourceBuilder<ParameterResource> password,
    int port,
    string initScript)
{
    return builder
        .AddContainer(resourceName, "postgres", "18-alpine")
        .WithEnvironment("POSTGRES_DB", databaseName)
        .WithEnvironment("POSTGRES_USER", "postgres")
        .WithEnvironment("POSTGRES_PASSWORD", password)
        .WithEndpoint(port: port, targetPort: 5432, name: "postgres", isProxied: false)
        .WithBindMount(
            initScript,
            "/docker-entrypoint-initdb.d/10-runtime-role.sql",
            isReadOnly: true);
}
