using Aspire.Hosting.Testing;
using NUnit.Framework;

namespace DistributedCommerce.AppHost.Tests;

public sealed class LocalTopologyTests
{
    private static readonly TimeSpan ModelTimeout = TimeSpan.FromMinutes(2);

    [Test]
    [Category("Topology")]
    public async Task AppHost_Declares_Expected_Secure_Topology()
    {
        Environment.SetEnvironmentVariable(
            "ASPIRE_DCP_USE_DEVELOPER_CERTIFICATE",
            "false");
        Environment.SetEnvironmentVariable(
            "ASPIRE_VERSION_CHECK_DISABLED",
            "true");

        using var cancellation = new CancellationTokenSource(ModelTimeout);

        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.DistributedCommerce_AppHost>(
                cancellationToken: cancellation.Token);

        var resourceNames = appHost.Resources
            .Select(resource => resource.Name)
            .ToArray();

        var requiredPlatformResources = new[]
        {
            "api-gateway",
            "customers-api",
            "customers-db",
            "customers-migrator",
            "products-api",
            "products-db",
            "products-migrator",
            "inventory-db",
            "inventory-migrator",
            "inventory-service",
            "keycloak",
            "notifications-service",
            "orders-api",
            "orders-db",
            "orders-migrator",
            "payments-db",
            "payments-migrator",
            "payments-service",
            "rabbitmq",
            "vault",
            "vault-init"
        };

        var requiredSecretResources = new[]
        {
            "customers-db-password",
            "products-db-password",
            "inventory-db-password",
            "keycloak-admin-password",
            "orders-db-password",
            "payments-db-password",
            "rabbitmq-password",
            "vault-dev-root-token"
        };

        Assert.Multiple(() =>
        {
            foreach (var resourceName in requiredPlatformResources)
                Assert.That(resourceNames, Does.Contain(resourceName), $"Missing platform resource '{resourceName}'.");

            foreach (var resourceName in requiredSecretResources)
                Assert.That(resourceNames, Does.Contain(resourceName), $"Missing secret resource '{resourceName}'.");

            Assert.That(
                resourceNames.Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(resourceNames.Length),
                "Aspire resource names must be unique.");
        });
    }
}
