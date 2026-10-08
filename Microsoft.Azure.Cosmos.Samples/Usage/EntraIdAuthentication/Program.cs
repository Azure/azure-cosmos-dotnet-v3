namespace Cosmos.Samples.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using global::Azure.Core;
    using global::Azure.Identity;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Extensions.Configuration;

    // ----------------------------------------------------------------------------------------------------------
    // Prerequisites -
    //
    // 1. An Azure Cosmos account -
    //    https://docs.microsoft.com/azure/cosmos-db/create-cosmosdb-resources-portal
    //
    // 2. An Azure AD (Microsoft Entra ID) identity that has been granted a Cosmos DB
    //    data-plane role on the account (for example "Cosmos DB Built-in Data Contributor").
    //    https://learn.microsoft.com/azure/cosmos-db/how-to-setup-rbac
    //
    // 3. A database and container that ALREADY exist on the account. With a data-plane RBAC
    //    role you cannot create them from the SDK (see RunControlPlaneLimitationDemoAsync below) -
    //    create them ahead of time via ARM / Bicep / the Azure portal / the Azure CLI, e.g.:
    //
    //      az cosmosdb sql database create   -a <account> -g <rg> -n SampleDb
    //      az cosmosdb sql container create  -a <account> -g <rg> -d SampleDb -n People --partition-key-path /id
    //
    // 4. Microsoft.Azure.Cosmos and Azure.Identity NuGet packages.
    // ----------------------------------------------------------------------------------------------------------
    // Sample - demonstrates connecting a CosmosClient using Azure AD (Microsoft Entra ID) via an
    //          Azure.Core TokenCredential instead of an account key, and the practical differences
    //          you hit when running on a data-plane RBAC token:
    //
    //          * Data-plane operations (item create / read / query / replace / delete) work.
    //          * Control-plane / metadata operations (create database, create container, ...) are
    //            rejected with 403 Forbidden - they must be done through ARM, not the data plane.
    //
    //          The sample also shows two alternative construction patterns: a custom background
    //          token-refresh interval and the CosmosClientBuilder fluent API.
    // ----------------------------------------------------------------------------------------------------------

    public class Program
    {
        private const string DatabaseId = "SampleDb";
        private const string ContainerId = "People";

        // <Main>
        public static async Task Main(string[] _)
        {
            try
            {
                IConfigurationRoot configuration = new ConfigurationBuilder()
                    .AddJsonFile("appSettings.json")
                    .Build();

                string endpoint = configuration["EndPointUrl"];
                if (string.IsNullOrEmpty(endpoint) || string.Equals(endpoint, "https://localhost:8081"))
                {
                    throw new ArgumentNullException(
                        "Please specify the endpoint of a real, RBAC-enabled Cosmos account in appSettings.json. " +
                        "The emulator does not issue or validate real Entra tokens.");
                }

                // DefaultAzureCredential resolves a token from the environment, a managed identity,
                // the Azure CLI, Visual Studio, etc. Any Azure.Core TokenCredential works here.
                TokenCredential tokenCredential = new DefaultAzureCredential();

                // The simplest way to authenticate with Azure AD: pass a TokenCredential to the
                // CosmosClient. The SDK acquires a token for the account scope and refreshes it in
                // the background before expiry.
                // <CreateClientWithTokenCredential>
                using CosmosClient client = new CosmosClient(endpoint, tokenCredential);
                // </CreateClientWithTokenCredential>

                Console.WriteLine($"Created CosmosClient for '{client.Endpoint}' using a TokenCredential.");

                // Show the control-plane vs data-plane behavior difference, then run real data operations.
                await Program.RunControlPlaneLimitationDemoAsync(client);
                await Program.RunDataPlaneDemoAsync(client);

                // Alternative construction patterns.
                Program.ShowCustomRefreshIntervalConstruction(endpoint, tokenCredential);
                Program.ShowBuilderConstruction(endpoint, tokenCredential);
            }
            catch (CosmosException cre)
            {
                Console.WriteLine(cre.ToString());
            }
            catch (Exception e)
            {
                Exception baseException = e.GetBaseException();
                Console.WriteLine("Error: {0}, Message: {1}", e.Message, baseException.Message);
            }
            finally
            {
                Console.WriteLine("End of demo, press any key to exit.");
                Console.ReadKey();
            }
        }
        // </Main>

        /// <summary>
        /// The most commonly misunderstood aspect of AAD with Cosmos DB: a data-plane RBAC role
        /// (such as "Cosmos DB Built-in Data Contributor") grants access to data, NOT to resource
        /// management. Metadata / control-plane operations - creating or deleting databases and
        /// containers, changing throughput, reading account keys - are governed by Azure RBAC on the
        /// ARM control plane, so they are rejected with 403 Forbidden when attempted over a
        /// data-plane token. Provision databases and containers with ARM / Bicep / the portal / the
        /// Azure CLI instead.
        /// </summary>
        private static async Task RunControlPlaneLimitationDemoAsync(CosmosClient client)
        {
            // <ControlPlaneLimitation>
            try
            {
                // This is a metadata (control-plane) operation. It will succeed with a master key,
                // but with a data-plane RBAC token it is rejected.
                await client.CreateDatabaseIfNotExistsAsync(Program.DatabaseId);

                Console.WriteLine(
                    $"Created (or found) database '{Program.DatabaseId}'. " +
                    "If you see this, the credential carries control-plane (ARM) permissions, " +
                    "not just a data-plane RBAC role.");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
            {
                Console.WriteLine(
                    "CreateDatabaseIfNotExistsAsync was rejected with 403 Forbidden - this is expected " +
                    "with a data-plane RBAC role. Create databases and containers through ARM / Bicep / " +
                    "the Azure portal / the Azure CLI; the SDK token is only for data operations.");
            }
            // </ControlPlaneLimitation>
        }

        /// <summary>
        /// Data-plane item operations (create, read, query, replace, delete) are exactly what a
        /// data-plane RBAC role authorizes, so these run normally over an AAD token. The database and
        /// container are assumed to already exist (see the prerequisites and the control-plane demo).
        /// </summary>
        private static async Task RunDataPlaneDemoAsync(CosmosClient client)
        {
            Container container;
            try
            {
                container = client.GetContainer(Program.DatabaseId, Program.ContainerId);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                Console.WriteLine(
                    $"Container '{Program.DatabaseId}/{Program.ContainerId}' was not found. Create it via " +
                    "ARM / the Azure CLI before running the data-plane demo (data-plane RBAC cannot create it).");
                return;
            }

            string id = Guid.NewGuid().ToString();
            Person person = new Person
            {
                Id = id,
                Name = "Ada Lovelace",
                City = "London"
            };

            // <DataPlaneOperations>
            // Create
            ItemResponse<Person> created = await container.CreateItemAsync(
                person,
                new PartitionKey(person.Id));
            Console.WriteLine(
                $"Created item '{created.Resource.Id}' (RU charge: {created.RequestCharge:0.##}).");

            // Read
            ItemResponse<Person> read = await container.ReadItemAsync<Person>(
                id,
                new PartitionKey(id));
            Console.WriteLine($"Read item '{read.Resource.Id}' for '{read.Resource.Name}'.");

            // Query
            QueryDefinition query = new QueryDefinition(
                "SELECT * FROM c WHERE c.city = @city")
                .WithParameter("@city", person.City);
            using FeedIterator<Person> iterator = container.GetItemQueryIterator<Person>(query);
            while (iterator.HasMoreResults)
            {
                FeedResponse<Person> page = await iterator.ReadNextAsync();
                Console.WriteLine($"Query returned {page.Count} item(s) in '{person.City}'.");
            }

            // Replace
            read.Resource.City = "Cambridge";
            await container.ReplaceItemAsync(read.Resource, id, new PartitionKey(id));
            Console.WriteLine($"Replaced item '{id}' (city updated).");

            // Delete (clean up the item this sample created)
            await container.DeleteItemAsync<Person>(id, new PartitionKey(id));
            Console.WriteLine($"Deleted item '{id}'.");
            // </DataPlaneOperations>
        }

        /// <summary>
        /// Controls how aggressively the background token refresh runs. The default is 50% of the
        /// token's lifetime; set <see cref="CosmosClientOptions.TokenCredentialBackgroundRefreshInterval"/>
        /// to override it. The recommended minimum is 5 minutes.
        /// </summary>
        private static void ShowCustomRefreshIntervalConstruction(
            string endpoint,
            TokenCredential tokenCredential)
        {
            // <CreateClientWithCustomRefreshInterval>
            CosmosClientOptions clientOptions = new CosmosClientOptions
            {
                TokenCredentialBackgroundRefreshInterval = TimeSpan.FromMinutes(30)
            };

            using CosmosClient client = new CosmosClient(endpoint, tokenCredential, clientOptions);
            // </CreateClientWithCustomRefreshInterval>
            Console.WriteLine(
                $"Created CosmosClient with a background token refresh interval of " +
                $"{clientOptions.TokenCredentialBackgroundRefreshInterval}.");
        }

        /// <summary>
        /// CosmosClientBuilder also accepts a TokenCredential for fluent configuration.
        /// </summary>
        private static void ShowBuilderConstruction(
            string endpoint,
            TokenCredential tokenCredential)
        {
            // <CreateClientWithBuilder>
            CosmosClientBuilder builder = new CosmosClientBuilder(endpoint, tokenCredential)
                .WithApplicationName("EntraIdAuthenticationSample");

            using CosmosClient client = builder.Build();
            // </CreateClientWithBuilder>

            // If your account requires a non-default token scope, set the environment variable
            // AZURE_COSMOS_AAD_SCOPE_OVERRIDE before creating the client. When it is not set, the
            // SDK requests "https://{account-host}/.default" and falls back to
            // "https://cosmos.azure.com/.default" if the resource scope is rejected (AADSTS500011).
            Console.WriteLine($"Created CosmosClient for '{client.Endpoint}' using CosmosClientBuilder.");
        }

        private sealed class Person
        {
            [Newtonsoft.Json.JsonProperty("id")]
            public string Id { get; set; }

            [Newtonsoft.Json.JsonProperty("name")]
            public string Name { get; set; }

            [Newtonsoft.Json.JsonProperty("city")]
            public string City { get; set; }
        }
    }
}
