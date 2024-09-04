namespace Microsoft.Azure.Cosmos.FaultInjection.Tests.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Fluent;

    internal static class TestCommon
    {
        public const string FaultInjectionContainerName = "faultInjectionContainer";
        public const string FaultInjectionDatabaseName = "faultInjectionDatabase";

        public const string EndpointMultiRegion = "";
        public const string AuthKeyMultiRegion = "";
        
        internal static CosmosClient CreateCosmosClient(
            bool useGateway,
            FaultInjector injector,
            bool multiRegion,
            List<string>? preferredRegion = null,
            Action<CosmosClientBuilder>? customizeClientBuilder = null)
        {
            CosmosClientBuilder cosmosClientBuilder = GetDefaultConfiguration(multiRegion);
            cosmosClientBuilder.WithFaultInjection(injector.GetChaosInterceptorFactory());

            customizeClientBuilder?.Invoke(cosmosClientBuilder);

            if (useGateway)
            {
                cosmosClientBuilder.WithConnectionModeGateway();
            }

            if (preferredRegion != null)
            {
                cosmosClientBuilder.WithApplicationPreferredRegions(preferredRegion);
            }

            return cosmosClientBuilder.Build();
        }

        internal static CosmosClient CreateCosmosClient(
            bool useGateway,
            bool multiRegion,
            Action<CosmosClientBuilder>? customizeClientBuilder = null)
        {
            CosmosClientBuilder cosmosClientBuilder = GetDefaultConfiguration(multiRegion);

            customizeClientBuilder?.Invoke(cosmosClientBuilder);

            if (useGateway)
            {
                cosmosClientBuilder.WithConnectionModeGateway();
            }

            return cosmosClientBuilder.Build();
        }

        internal static CosmosClientBuilder GetDefaultConfiguration(
            bool multiRegion, 
            string? accountEndpointOverride = null)
        {
            CosmosClientBuilder clientBuilder = new CosmosClientBuilder(
                accountEndpoint: accountEndpointOverride 
                ?? EndpointMultiRegion,
                authKeyOrResourceToken: AuthKeyMultiRegion);
            
            if (!multiRegion)
            {
                return clientBuilder.WithApplicationPreferredRegions(new List<string> { "Central US" });
            }

            return clientBuilder;
        }

        internal static async Task<(Database, Container)> GetOrCreateMultiRegionFIDatabaseAndContainers(CosmosClient client)
        {
            Database database;
            Container container;

            DatabaseResponse db = await client.CreateDatabaseIfNotExistsAsync(
                id: TestCommon.FaultInjectionDatabaseName,
                throughput: 400);
            database = db.Database;

            if (db.StatusCode == HttpStatusCode.Created)
            {
                container = await database.CreateContainerIfNotExistsAsync(
                    id: TestCommon.FaultInjectionContainerName,
                    partitionKeyPath: "/pk",
                    throughput: 400);

                List<Task> tasks = new List<Task>()
                {
                    container.CreateItemAsync<FaultInjectionTestObject>(
                        new FaultInjectionTestObject { Id = "testId", Pk = "pk" }),
                    container.CreateItemAsync<FaultInjectionTestObject>(
                        new FaultInjectionTestObject { Id = "testId2", Pk = "pk2" }),
                    container.CreateItemAsync<FaultInjectionTestObject>(
                        new FaultInjectionTestObject { Id = "testId3", Pk = "pk3" }),
                    container.CreateItemAsync<FaultInjectionTestObject>(
                        new FaultInjectionTestObject { Id = "testId4", Pk = "pk4" })
                };

                await Task.WhenAll(tasks);

                //Must Ensure the data is replicated to all regions
                await Task.Delay(60000);

                return (database, container);
            }

            container = database.GetContainer(TestCommon.FaultInjectionContainerName);

            return (database, container);
        }

        internal class FaultInjectionTestObject
        {
            [JsonPropertyName("id")]
            public string? Id { get; set; }

            [JsonPropertyName("pk")]
            public string? Pk { get; set; }

            [JsonPropertyName("other")]
            public string? Other { get; set; }
        }
    }
}
