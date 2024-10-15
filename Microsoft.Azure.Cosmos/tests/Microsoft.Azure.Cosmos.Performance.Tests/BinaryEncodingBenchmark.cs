namespace Microsoft.Azure.Cosmos.Performance.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Configs;
    using BenchmarkDotNet.Columns;
    using BenchmarkDotNet.Exporters;
    using BenchmarkDotNet.Jobs;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json.Linq;
    using BenchmarkDotNet.Exporters.Csv;
    using System.Linq;

    [MemoryDiagnoser]
    [BenchmarkCategory("NewGateBenchmark")]
    [Config(typeof(CustomBenchmarkConfig))]
    public class BinaryEncodingBenchmark
    {
        private readonly string accountEndpoint = ""; // Replace with Cosmos DB endpoint
        private readonly string accountKey = ""; // Replace with Cosmos DB key
        private CosmosClient client;
        private Database database;
        private Container container;

        [GlobalSetup]
        public async Task SetUp()
        {
            Console.WriteLine("Setting up the BinaryEncodingBenchmark");
            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Direct,
                ApplicationPreferredRegions = new List<string>() { "West US2" }
            };

            this.client = new CosmosClient(accountEndpoint: this.accountEndpoint, authKeyOrResourceToken: this.accountKey, clientOptions: clientOptions);
            this.database = await this.client.CreateDatabaseIfNotExistsAsync("TestDatabase");

            ContainerResponse containerResponse = await this.database.CreateContainerIfNotExistsAsync(
                id: "BenchmarkContainer",
                partitionKeyPath: "/id",
                throughput: 10000);

            this.container = containerResponse.Container;

            if (containerResponse.StatusCode == HttpStatusCode.Created)
            {
                // Create fewer items to reduce workload and execution time
                List<Task> tasks = new List<Task>();
                for (int i = 0; i < 500; i++) 
                {
                    tasks.Add(this.container.CreateItemAsync(
                        item: new JObject()
                        {
                        { "id", i.ToString() },
                        { "name", Guid.NewGuid().ToString() },
                        { "otherdata", Guid.NewGuid().ToString() },
                        },
                        partitionKey: new PartitionKey(i.ToString())));
                }
                await Task.WhenAll(tasks);
            }
        }

        [GlobalCleanup]
        public async Task CleanupAsync()
        {
            Console.WriteLine("Cleaning up the BinaryEncodingBenchmark, except for the database.");
            this.client.Dispose();
        }

        [Benchmark]
        public async Task ReadItemsAsync()
        {
            await Parallel.ForEachAsync(Enumerable.Range(0, 500), async (i, _) => await this.container.ReadItemAsync<JObject>(
                    partitionKey: new PartitionKey(i.ToString()),
                    id: i.ToString()));
        }

        [Benchmark]
        public async Task QueryItemsAsync()
        {
            FeedIterator feedIterator = this.container.GetItemQueryStreamIterator(
                "SELECT * FROM c",
                requestOptions: new QueryRequestOptions()
                {
                    MaxConcurrency = 10, 
                    MaxItemCount = 100, 
                });

            while (feedIterator.HasMoreResults)
            {
                using (ResponseMessage response = await feedIterator.ReadNextAsync())
                {
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        [Benchmark]
        public async Task UpsertItemsAsync()
        {
            await Parallel.ForEachAsync(Enumerable.Range(0, 500), async (i, _) =>
            {
                JObject updatedItem = new JObject()
            {
                { "id", i.ToString() },
                { "name", $"UpdatedName_{i}" },
                { "otherdata", $"UpdatedData_{Guid.NewGuid()}" }
            };

                await this.container.UpsertItemAsync(
                    item: updatedItem,
                    partitionKey: new PartitionKey(i.ToString()));
            });
        }

        [Benchmark]
        public async Task DeleteItemsAsync()
        {
            await Parallel.ForEachAsync(Enumerable.Range(0, 500), async (i, _) =>
            {
                try
                {
                    await this.container.DeleteItemAsync<JObject>(
                        partitionKey: new PartitionKey(i.ToString()),
                        id: i.ToString());
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                }
            });
        }

        // Custom configuration 
        private class CustomBenchmarkConfig : ManualConfig
        {
            public CustomBenchmarkConfig()
            {
                this.AddColumn(StatisticColumn.OperationsPerSecond);  // Show RPS
                this.AddColumn(StatisticColumn.P95);

              
                this.AddJob(Job.Default
                    .WithLaunchCount(1) 
                    .WithWarmupCount(3) 
                    .WithIterationCount(5) 
                    .WithStrategy(BenchmarkDotNet.Engines.RunStrategy.Throughput));

                this.AddExporter(HtmlExporter.Default);
                this.AddExporter(CsvExporter.Default);
            }
        }
    }
}
