//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Benchmarks
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Columns;
    using BenchmarkDotNet.Configs;
    using BenchmarkDotNet.Diagnosers;
    using BenchmarkDotNet.Exporters;
    using BenchmarkDotNet.Exporters.Csv;
    using BenchmarkDotNet.Jobs;

    [MemoryDiagnoser]
    [BenchmarkCategory("ThinClientIntegrationBenchmark")]
    [Config(typeof(CustomBenchmarkConfig))]
    public class ThinClientIntegrationBenchmark
    {
        private CosmosClient client;
        private Database database;
        private Container container;
        private CosmosSystemTextJsonSerializer cosmosSystemTextJsonSerializer;
        private List<CosmosIntegrationTestObject> items;
        private readonly Random random = new();
        private List<CosmosIntegrationTestObject> deleteItems;
        private List<CosmosIntegrationTestObject> deleteStreamItems;

        private const int ItemCount = 1000;

        [GlobalSetup]
        public async Task GlobalSetup()
        {
            string connectionString = Environment.GetEnvironmentVariable("COSMOSDB_THINCLIENT");
            Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, "True");

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("COSMOSDB_THINCLIENT environment variable is not set.");
            }

            JsonSerializerOptions jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = null,
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            this.cosmosSystemTextJsonSerializer = new CosmosSystemTextJsonSerializer(jsonOptions);

            this.client = new CosmosClient(
                connectionString,
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    Serializer = this.cosmosSystemTextJsonSerializer
                });

            string dbName = "TestDb_" + Guid.NewGuid();
            string containerName = "TestContainer_" + Guid.NewGuid();

            this.database = await this.client.CreateDatabaseIfNotExistsAsync(dbName);
            this.container = await this.database.CreateContainerIfNotExistsAsync(containerName, "/pk");

            string pk = "pk_benchmark";
            this.items = this.GenerateItems(pk).ToList();

            foreach (CosmosIntegrationTestObject item in this.items)
            {
                await this.container.CreateItemAsync(item, new PartitionKey(item.Pk));
            }
        }

        [GlobalCleanup]
        public async Task GlobalCleanup()
        {
            Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, "False");
            if (this.database != null)
            {
                await this.database.DeleteAsync();
            }
            this.client?.Dispose();
        }

        private IEnumerable<CosmosIntegrationTestObject> GenerateItems(string partitionKey)
        {
            for (int i = 0; i < ItemCount; i++)
            {
                yield return new CosmosIntegrationTestObject
                {
                    Id = Guid.NewGuid().ToString(),
                    Pk = partitionKey,
                    Other = "Benchmark Item " + i
                };
            }
        }

        [Benchmark]
        public async Task CreateItemAsync()
        {
            CosmosIntegrationTestObject item = new CosmosIntegrationTestObject
            {
                Id = Guid.NewGuid().ToString(),
                Pk = "pk_benchmark",
                Other = "Create Test"
            };

            ItemResponse<CosmosIntegrationTestObject> response = await this.container.CreateItemAsync(item, new PartitionKey(item.Pk));
            if (response.StatusCode != HttpStatusCode.Created)
            {
                throw new Exception($"Failed to create item: {item.Id}");
            }
        }

        [Benchmark]
        public async Task CreateItemStreamAsync()
        {
            CosmosIntegrationTestObject item = new CosmosIntegrationTestObject
            {
                Id = Guid.NewGuid().ToString(),
                Pk = "pk_benchmark",
                Other = "Create Stream Test"
            };

            using Stream stream = this.cosmosSystemTextJsonSerializer.ToStream(item);
            ResponseMessage response = await this.container.CreateItemStreamAsync(stream, new PartitionKey(item.Pk));
            if (response.StatusCode != HttpStatusCode.Created)
            {
                throw new Exception($"Failed to create stream item: {item.Id}");
            }
        }

        [Benchmark]
        public async Task ReadItemAsync()
        {
            CosmosIntegrationTestObject item = this.items[this.random.Next(this.items.Count)];
            ItemResponse<CosmosIntegrationTestObject> response = await this.container.ReadItemAsync<CosmosIntegrationTestObject>(item.Id, new PartitionKey(item.Pk));
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception($"Failed to read item: {item.Id}");
            }
        }

        [Benchmark]
        public async Task ReadItemStreamAsync()
        {
            CosmosIntegrationTestObject item = this.items[this.random.Next(this.items.Count)];
            ResponseMessage response = await this.container.ReadItemStreamAsync(item.Id, new PartitionKey(item.Pk));
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception($"Failed to read stream item: {item.Id}");
            }
        }

        [Benchmark]
        public async Task ReplaceItemAsync()
        {
            CosmosIntegrationTestObject original = this.items[this.random.Next(this.items.Count)];
            original.Other = "Updated Other";
            ItemResponse<CosmosIntegrationTestObject> response = await this.container.ReplaceItemAsync(original, original.Id, new PartitionKey(original.Pk));
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception($"Failed to replace item: {original.Id}");
            }
        }

        [Benchmark]
        public async Task ReplaceItemStreamAsync()
        {
            CosmosIntegrationTestObject original = this.items[this.random.Next(this.items.Count)];
            original.Other = "Updated Stream Other";

            using Stream stream = this.cosmosSystemTextJsonSerializer.ToStream(original);
            ResponseMessage response = await this.container.ReplaceItemStreamAsync(stream, original.Id, new PartitionKey(original.Pk));
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception($"Failed to replace stream item: {original.Id}");
            }
        }

        [Benchmark]
        public async Task UpsertItemAsync()
        {
            CosmosIntegrationTestObject item = this.items[this.random.Next(this.items.Count)];
            item.Other = "Upserted";

            ItemResponse<CosmosIntegrationTestObject> response = await this.container.UpsertItemAsync(item, new PartitionKey(item.Pk));
            if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.Created)
            {
                throw new Exception($"Failed to upsert item: {item.Id}");
            }
        }

        [Benchmark]
        public async Task UpsertItemStreamAsync()
        {
            CosmosIntegrationTestObject item = this.items[this.random.Next(this.items.Count)];
            item.Other = "Upserted Stream";

            using Stream stream = this.cosmosSystemTextJsonSerializer.ToStream(item);
            ResponseMessage response = await this.container.UpsertItemStreamAsync(stream, new PartitionKey(item.Pk));
            if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.Created)
            {
                throw new Exception($"Failed to upsert stream item: {item.Id}");
            }
        }

        [IterationSetup(Target = nameof(DeleteItemAsync))]
        public void IterationSetupDeleteItem()
        {
            string pk = "pk_delete";
            this.deleteItems = this.GenerateItems(pk).Take(100).ToList();

            foreach (CosmosIntegrationTestObject item in this.deleteItems)
            {
                this.container.CreateItemAsync(item, new PartitionKey(item.Pk)).GetAwaiter().GetResult();
            }
        }

        [IterationSetup(Target = nameof(DeleteItemStreamAsync))]
        public void IterationSetupDeleteItemStream()
        {
            string pk = "pk_delete_stream";
            this.deleteStreamItems = this.GenerateItems(pk).Take(100).ToList();

            foreach (CosmosIntegrationTestObject item in this.deleteStreamItems)
            {
                this.container.CreateItemAsync(item, new PartitionKey(item.Pk)).GetAwaiter().GetResult();
            }
        }

        [Benchmark]
        public async Task DeleteItemAsync()
        {
            if (this.deleteItems.Count == 0) return;

            int index = this.random.Next(this.deleteItems.Count);
            CosmosIntegrationTestObject item = this.deleteItems[index];

            ItemResponse<CosmosIntegrationTestObject> response = await this.container.DeleteItemAsync<CosmosIntegrationTestObject>(item.Id, new PartitionKey(item.Pk));
            if (response.StatusCode != HttpStatusCode.NoContent)
            {
                throw new Exception($"Failed to delete item: {item.Id}");
            }

            this.deleteItems.RemoveAt(index);
        }

        [Benchmark]
        public async Task DeleteItemStreamAsync()
        {
            if (this.deleteStreamItems.Count == 0) return;

            int index = this.random.Next(this.deleteStreamItems.Count);
            CosmosIntegrationTestObject item = this.deleteStreamItems[index];

            ResponseMessage response = await this.container.DeleteItemStreamAsync(item.Id, new PartitionKey(item.Pk));
            if (response.StatusCode != HttpStatusCode.NoContent)
            {
                throw new Exception($"Failed to delete stream item: {item.Id}");
            }

            this.deleteStreamItems.RemoveAt(index);
        }

        [Benchmark]
        public async Task BulkCreateItemsAsync()
        {
            string pk = "pk_bulk";
            List<CosmosIntegrationTestObject> bulkItems = this.GenerateItems(pk).Take(100).ToList();
            List<Task> tasks = new();

            foreach (CosmosIntegrationTestObject item in bulkItems)
            {
                tasks.Add(this.container.CreateItemAsync(item, new PartitionKey(item.Pk)));
            }

            await Task.WhenAll(tasks);
        }

        [Benchmark]
        public async Task TransactionalBatchCreateAsync()
        {
            string pk = "pk_batch";
            List<CosmosIntegrationTestObject> batchItems = this.GenerateItems(pk).Take(10).ToList();

            TransactionalBatch batch = this.container.CreateTransactionalBatch(new PartitionKey(pk));
            foreach (CosmosIntegrationTestObject item in batchItems)
            {
                batch.CreateItem(item);
            }
            TransactionalBatchResponse response = await batch.ExecuteAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Transactional batch failed.");
            }
        }

        [Benchmark]
        public async Task QueryItemsAsync()
        {
            FeedIterator<CosmosIntegrationTestObject> query = this.container.GetItemQueryIterator<CosmosIntegrationTestObject>(
                $"SELECT * FROM c WHERE c.pk = 'pk_benchmark'");
            List<CosmosIntegrationTestObject> results = new();
            while (query.HasMoreResults)
            {
                FeedResponse<CosmosIntegrationTestObject> response = await query.ReadNextAsync();
                results.AddRange(response);
            }
            if (results.Count == 0)
            {
                throw new Exception("Query returned no results.");
            }
        }

        [Benchmark]
        public async Task QueryItemsStreamAsync()
        {
            FeedIterator query = this.container.GetItemQueryStreamIterator(
                $"SELECT * FROM c WHERE c.pk = 'pk_benchmark'");
            while (query.HasMoreResults)
            {
                using ResponseMessage response = await query.ReadNextAsync();
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception("QueryStream failed.");
                }
            }
        }
        private class CustomBenchmarkConfig : ManualConfig
        {
            public CustomBenchmarkConfig()
            {
                this.AddColumn(StatisticColumn.OperationsPerSecond);
                this.AddColumn(StatisticColumn.P95);
                this.AddColumn(StatisticColumn.P100);

                this.AddDiagnoser(MemoryDiagnoser.Default);
                this.AddDiagnoser(ThreadingDiagnoser.Default);

                this.AddColumnProvider(DefaultConfig.Instance.GetColumnProviders().ToArray());

                this.AddJob(Job.ShortRun.WithStrategy(BenchmarkDotNet.Engines.RunStrategy.Throughput));

                this.AddExporter(HtmlExporter.Default);
                this.AddExporter(CsvExporter.Default);
            }
        }
    }
    internal class CosmosIntegrationTestObject
    {

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("pk")]
        public string Pk { get; set; }

        [JsonPropertyName("other")]
        public string Other { get; set; }
    }
}