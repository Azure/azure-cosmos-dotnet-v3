// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos.Diagnostics;

    [Config(typeof(SdkBenchmarkConfiguration))]
    public class ReplicaValidationBenchmark
    {
        private Database database;

        private Container container;

        /*[Params(1, 2, 3, 4)]
        public int IterationCount { get; set; }*/

        [GlobalSetup]
        public async Task Setup()
        {
            string partitionKey = "/pk";
            string databaseName = "UpgradeResiliencyTestDatabase" + (DateTime.UtcNow.Millisecond % 10000);
            string containerName = "ReplicaValidationContainer" + (DateTime.UtcNow.Millisecond % 10000);
            string endpoint = "https://localhost:8081/";
            string authKey = "<tbd>";
            string connectionString = $"AccountEndpoint={endpoint};AccountKey={authKey};";

            Environment.SetEnvironmentVariable("AZURE_COSMOS_REPLICA_VALIDATION_ENABLED", "true");
            CosmosClientOptions clientOptions = new()
            {
                ApplicationPreferredRegions = new List<string>()
                {
                    Regions.WestCentralUS,
                    Regions.EastUS2,
                    Regions.WestUS,
                 },
            };
            
            CosmosClient cosmosClient = new (
                connectionString: connectionString,
                clientOptions: clientOptions);

            DatabaseResponse databaseResponse = await cosmosClient.CreateDatabaseAsync(
                   id: databaseName);

            ContainerResponse containerResponse = await databaseResponse.Database.CreateContainerAsync(
                containerProperties: new ContainerProperties(
                    id: containerName,
                    partitionKeyPath: partitionKey),
                throughput: 20000,
                cancellationToken: CancellationToken.None);

            await Task.Delay(4000);
            this.database = databaseResponse.Database;
            this.container = containerResponse.Container;
        }

        [GlobalCleanup]
        public async Task Cleanup()
        {
            try
            {
                await this.container.DeleteContainerAsync();
                await this.database.DeleteAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        [Benchmark]
        [BenchmarkCategory("GateBenchmark")]
        public async Task CosmosEndToEndOperations()
        {
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    ToDoActivity activityItem = ToDoActivity.CreateRandomToDoActivity(randomTaskNumber: true);

                    // Create Item.
                    ItemResponse<ToDoActivity> createResponse = await this.container.CreateItemAsync<ToDoActivity>(activityItem);
                    
                    // Read Item.
                    ItemResponse<ToDoActivity> readResponse = await this.container.ReadItemAsync<ToDoActivity>(activityItem.id, new PartitionKey(activityItem.pk));
                    
                    // Upsert Item.
                    ItemResponse<ToDoActivity> upsertResponse = await this.container.UpsertItemAsync<ToDoActivity>(ReplicaValidationBenchmark.UpdateTodoActivity(activityItem));

                    // Query Item.
                    QueryDefinition parameterizedQuery = new QueryDefinition(
                        query: "SELECT * FROM p WHERE p.CamelCase = @case"
                        )
                        .WithParameter("@case", "updatedCase");

                    using FeedIterator<ToDoActivity> filteredFeed = this.container
                        .GetItemQueryIterator<ToDoActivity>(
                            queryDefinition: parameterizedQuery);

                    while (filteredFeed.HasMoreResults)
                    {
                        FeedResponse<ToDoActivity> feedResponse = await filteredFeed.ReadNextAsync();
                        foreach (ToDoActivity item in feedResponse)
                        {
                            Console.WriteLine($"Fetched item with id:\t{item.id}");
                        }
                    }

                    // Delete Item.
                    ItemResponse<ToDoActivity> deleteResponse = await this.container.DeleteItemAsync<ToDoActivity>(activityItem.id, new PartitionKey(activityItem.pk));
                }
                catch (CosmosException ex)
                {
                    CosmosTraceDiagnostics diagnostics = (CosmosTraceDiagnostics)ex.Diagnostics;
                    string diagnosticString = diagnostics.ToString();
                    Console.WriteLine(diagnosticString);
                }
            }
        }

        private static ToDoActivity UpdateTodoActivity(ToDoActivity activityItem)
        {
            activityItem.CamelCase = "updatedCase";
            return activityItem;
        }

        public class ToDoActivity
        {
#pragma warning disable IDE1006 // Naming Styles
            public string id { get; set; }


            public int taskNum { get; set; }

            public double cost { get; set; }

            public string description { get; set; }

            public string pk { get; set; }

            public string CamelCase { get; set; }

            public int? nullableInt { get; set; }

            public bool valid { get; set; }

            public ToDoActivity[] children { get; set; }
#pragma warning restore IDE1006 // Naming Styles

            public override bool Equals(Object obj)
            {
                if (obj is not ToDoActivity input)
                {
                    return false;
                }

                return string.Equals(this.id, input.id)
                    && this.taskNum == input.taskNum
                    && this.cost == input.cost
                    && string.Equals(this.description, input.description)
                    && string.Equals(this.pk, input.pk);
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }

            public static async Task<IList<ToDoActivity>> CreateRandomItems(
                Container container,
                int pkCount,
                int perPKItemCount = 1,
                bool randomPartitionKey = true,
                bool randomTaskNumber = false)
            {
                List<ToDoActivity> createdList = new List<ToDoActivity>();
                for (int i = 0; i < pkCount; i++)
                {
                    string pk = "PKC";
                    if (randomPartitionKey)
                    {
                        pk += Guid.NewGuid().ToString();
                    }

                    for (int j = 0; j < perPKItemCount; j++)
                    {
                        ToDoActivity temp = ToDoActivity.CreateRandomToDoActivity(
                            pk: pk,
                            id: null,
                            randomTaskNumber: randomTaskNumber);

                        createdList.Add(temp);

                        await container.CreateItemAsync<ToDoActivity>(item: temp);
                    }
                }

                return createdList;
            }

            public static ToDoActivity CreateRandomToDoActivity(
                string pk = null,
                string id = null,
                bool randomTaskNumber = false)
            {
                if (string.IsNullOrEmpty(pk))
                {
                    pk = "PKC" + Guid.NewGuid().ToString();
                }
                id ??= Guid.NewGuid().ToString();

                int taskNum = 42;
                if (randomTaskNumber)
                {
                    taskNum = Random.Shared.Next();
                }

                return new ToDoActivity()
                {
                    id = id,
                    description = "CreateRandomToDoActivity",
                    pk = pk,
                    taskNum = taskNum,
                    cost = double.MaxValue,
                    CamelCase = "camelCase",
                    children = new ToDoActivity[]
                    { new ToDoActivity { id = "child1", taskNum = 30 },
                  new ToDoActivity { id = "child2", taskNum = 40}
                    },
                    valid = true,
                    nullableInt = null
                };
            }
        }
    }
}