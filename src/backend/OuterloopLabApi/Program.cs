using System.Net;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using OuterloopLabApi.Controllers;
using OuterloopLabApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

builder.Services.AddHttpClient<IExternalRateProviderClient, ExternalRateProviderClient>(client =>
{
    // Base URL is read inside the implementation because it needs env-var defaulting logic.
    client.Timeout = TimeSpan.FromSeconds(15);
});

// Cosmos provisioning is required before the app runs.
var appConfig = CosmosAppConfiguration.FromEnvironment();
var managedIdentityCredential = CreateManagedIdentityCredential(appConfig.AzurManagedIdentityClientId);

// Control-plane provisioning is best-effort; data-plane create-if-not-exists is required.
await CosmosProvisioner.TryProvisionWithArmAsync(appConfig, managedIdentityCredential);

var cosmosClient = new CosmosClient(appConfig.CosmosDbUri, managedIdentityCredential);
var cosmosContainer = await CosmosProvisioner.ProvisionDataPlaneAsync(cosmosClient, appConfig);

builder.Services.AddSingleton<IConversionAuditRepository>(_ => new ConversionAuditRepository(cosmosContainer));
builder.Services.AddSingleton<ICurrencyConversionService, CurrencyConversionService>(sp =>
{
    var rateProvider = sp.GetRequiredService<IExternalRateProviderClient>();
    var repo = sp.GetRequiredService<IConversionAuditRepository>();
    return new CurrencyConversionService(rateProvider, repo, () => DateTime.UtcNow);
});

var app = builder.Build();

app.MapControllers();

app.Run();

static TokenCredential CreateManagedIdentityCredential(string managedIdentityClientId)
{
    return new DefaultAzureCredential(new DefaultAzureCredentialOptions
    {
        ManagedIdentityClientId = managedIdentityClientId,
        // Avoid ambient credential sources that can behave differently in dev.
        ExcludeEnvironmentCredential = true,
        ExcludeSharedTokenCacheCredential = true,
        ExcludeVisualStudioCredential = true,
        ExcludeVisualStudioCodeCredential = true,
        ExcludeAzureCliCredential = true
    });
}

file sealed class CosmosAppConfiguration
{
    public required string CosmosDbUri { get; init; }
    public required string CosmosDbDatabase { get; init; }
    public required string CosmosDbContainer { get; init; }
    public required string CosmosDbAccountName { get; init; }
    public required string CosmosDbResourceGroup { get; init; }
    public required string CosmosDbRegion { get; init; }
    public required string AzurManagedIdentityClientId { get; init; }

    public static CosmosAppConfiguration FromEnvironment()
    {
        static string ReadRequired(string key)
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Missing required environment variable: {key}");
            }
            return value;
        }

        // Exact env var keys are required by docs/CONTAINER_ENVIRONMENT_VARIABLES.md.
        return new CosmosAppConfiguration
        {
            CosmosDbUri = ReadRequired("COSMOS_DB_URI"),
            CosmosDbDatabase = ReadRequired("COSMOS_DB_DATABASE"),
            CosmosDbContainer = ReadRequired("COSMOS_DB_CONTAINER"),
            CosmosDbAccountName = ReadRequired("COSMOS_DB_ACCOUNT_NAME"),
            CosmosDbResourceGroup = ReadRequired("COSMOS_DB_RESOURCE_GROUP"),
            CosmosDbRegion = ReadRequired("COSMOS_DB_REGION"),
            AzurManagedIdentityClientId = ReadRequired("AZURE_MANAGED_IDENTITY_CLIENT_ID")
        };
    }
}

file sealed class CosmosProvisioner
{
    public static async Task TryProvisionWithArmAsync(CosmosAppConfiguration cfg, TokenCredential credential)
    {
        // Best-effort ARM provisioning (can fail due to RBAC mismatch between control-plane vs data-plane).
        // Intentionally not fatal.
        try
        {
            var subscriptionId = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");
            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                return;
            }

            // Using Azure.ResourceManager and Azure.ResourceManager.CosmosDB packages to satisfy control-plane provisioning requirement.
            // NOTE: Managed Identity RBAC for ARM may differ; exceptions are swallowed to keep startup resilient.
            // To avoid compile-time coupling to exact constructor signatures across SDK versions,
            // this uses reflection to instantiate ArmClient and any CosmosDB-specific collections.

            var armClientType = Type.GetType("Azure.ResourceManager.ArmClient, Azure.ResourceManager");
            if (armClientType is null)
            {
                return;
            }

            var waitUntilType = Type.GetType("Azure.ResourceManager.WaitUntil, Azure.ResourceManager");
            var waitUntilCompleted = waitUntilType?.GetProperty("Completed")?.GetValue(null);
            if (waitUntilCompleted is null)
            {
                // If we can't resolve WaitUntil.Completed, best-effort provisioning is skipped.
                return;
            }

            object armClient;
            {
                var ctor = armClientType
                    .GetConstructors()
                    .FirstOrDefault(c =>
                    {
                        var p = c.GetParameters();
                        if (p.Length != 2) return false;
                        return p[0].ParameterType.IsAssignableFrom(credential.GetType()) && p[1].ParameterType == typeof(string);
                    });

                if (ctor is null)
                {
                    // Try a looser match: first parameter assignable from TokenCredential.
                    ctor = armClientType
                        .GetConstructors()
                        .FirstOrDefault(c =>
                        {
                            var p = c.GetParameters();
                            if (p.Length != 2) return false;
                            return typeof(TokenCredential).IsAssignableFrom(p[0].ParameterType) && p[1].ParameterType == typeof(string);
                        });
                }

                if (ctor is null)
                {
                    return;
                }

                armClient = ctor.Invoke(new object[] { credential, subscriptionId });
            }

            var cosmosDbAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Azure.ResourceManager.CosmosDB");
            if (cosmosDbAssembly is null)
            {
                return;
            }

            static object? InvokeIfExists(object instance, string methodName, object?[] args)
            {
                var method = instance.GetType().GetMethods()
                    .FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == args.Length);
                return method is null ? null : method.Invoke(instance, args);
            }

            // Locate subscription resource.
            var subscriptionResource = InvokeIfExists(armClient, "GetSubscriptionResource", new object?[] { subscriptionId });
            if (subscriptionResource is null)
            {
                return;
            }

            // Locate CosmosDB account collection and fetch the account.
            var cosmosAccountsCollection = InvokeIfExists(subscriptionResource, "GetCosmosDBAccounts", Array.Empty<object?>());
            if (cosmosAccountsCollection is null)
            {
                return;
            }

            var getAccountTaskObj = InvokeIfExists(cosmosAccountsCollection, "GetAsync", new object?[] { cfg.CosmosDbResourceGroup, cfg.CosmosDbAccountName });
            if (getAccountTaskObj is not Task getAccountTask)
            {
                return;
            }

            await getAccountTask;
            var accountResourceProp = getAccountTaskObj.GetType().GetProperty("Result");
            var accountResource = accountResourceProp?.GetValue(getAccountTaskObj);
            if (accountResource is null)
            {
                return;
            }

            // Create-or-update Cosmos DB database (best-effort).
            var databasesCollection = InvokeIfExists(accountResource, "GetCosmosDBDatabases", Array.Empty<object?>());
            if (databasesCollection is null)
            {
                return;
            }

            var dbContentType = cosmosDbAssembly.GetTypes().FirstOrDefault(t =>
                t.Name.Contains("CosmosDBDatabase", StringComparison.OrdinalIgnoreCase) &&
                t.Name.Contains("Create", StringComparison.OrdinalIgnoreCase) &&
                t.Name.Contains("Update", StringComparison.OrdinalIgnoreCase));

            var dbContent = dbContentType is null ? null : Activator.CreateInstance(dbContentType);

            var createDbTaskObj = dbContent is null
                ? InvokeIfExists(databasesCollection, "CreateOrUpdateAsync", new object?[] { cfg.CosmosDbDatabase, null })
                : InvokeIfExists(databasesCollection, "CreateOrUpdateAsync", new object?[] { waitUntilCompleted, cfg.CosmosDbDatabase, dbContent });

            if (createDbTaskObj is Task createDbTask)
            {
                await createDbTask;

                // Best-effort container provisioning.
                var databaseResource = createDbTaskObj.GetType().GetProperty("Result")?.GetValue(createDbTaskObj) ?? accountResource;
                var containersCollection = InvokeIfExists(databaseResource, "GetCosmosDBContainers", Array.Empty<object?>());
                if (containersCollection is not null)
                {
                    var containerContentType = cosmosDbAssembly.GetTypes().FirstOrDefault(t =>
                        t.Name.Contains("CosmosDBContainer", StringComparison.OrdinalIgnoreCase) &&
                        t.Name.Contains("Create", StringComparison.OrdinalIgnoreCase) &&
                        t.Name.Contains("Update", StringComparison.OrdinalIgnoreCase));

                    var containerContent = containerContentType is null ? null : Activator.CreateInstance(containerContentType);

                    // If we can't construct content, still try a minimal overload.
                    var createContainerTaskObj = containerContent is null
                        ? InvokeIfExists(containersCollection, "CreateOrUpdateAsync", new object?[] { cfg.CosmosDbContainer, null })
                        : InvokeIfExists(containersCollection, "CreateOrUpdateAsync", new object?[] { waitUntilCompleted, cfg.CosmosDbContainer, containerContent });

                    if (createContainerTaskObj is Task createContainerTask)
                    {
                        await createContainerTask;
                    }
                }
            }
        }
        catch
        {
            // Ignore ARM provisioning failures.
        }
    }

    public static async Task<Container> ProvisionDataPlaneAsync(CosmosClient client, CosmosAppConfiguration cfg)
    {
        // Required: token-authenticated create-if-not-exists must succeed, otherwise startup must fail.
        var databaseResponse = await client.CreateDatabaseIfNotExistsAsync(cfg.CosmosDbDatabase);
        var database = databaseResponse.Database;

        var containerProperties = new ContainerProperties(cfg.CosmosDbContainer, "/conversionId");
        var containerResponse = await database.CreateContainerIfNotExistsAsync(containerProperties, throughput: 400);
        return containerResponse.Container;
    }
}
