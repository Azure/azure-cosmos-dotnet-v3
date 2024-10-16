namespace Microsoft.Azure.Cosmos.Performance.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Configs;
    using BenchmarkDotNet.Columns;
    using BenchmarkDotNet.Exporters;
    using BenchmarkDotNet.Exporters.Csv;
    using BenchmarkDotNet.Jobs;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System.Threading;
    using System.ComponentModel;

    [MemoryDiagnoser]
    [BenchmarkCategory("NewGateBenchmark")]
    [Config(typeof(CustomBenchmarkConfig))]
    public class BinaryEncodingBenchmark
    {

        private readonly string accountEndpoint = "";  // Replace with your Cosmos DB endpoint
        private readonly string accountKey = "";
        private CosmosClient client;
        private Database database;
      
        private Cosmos.Container container;

        [GlobalSetup]
        public async Task SetUp()
        {
            Console.WriteLine("Setting up the BinaryEncodingBenchmark");

        CosmosClientOptions clientOptions = new CosmosClientOptions
        {
            ApplicationPreferredRegions = new List<string> { "West US2" },
            AllowBulkExecution = true
        };

        this.client = new CosmosClient(this.accountEndpoint, this.accountKey, clientOptions);
        this.database = await this.client.CreateDatabaseIfNotExistsAsync("TestDatabase");

        ContainerResponse containerResponse = await this.database.CreateContainerIfNotExistsAsync(
            id: "BenchmarkContainer",
            partitionKeyPath: "/id",
            throughput: 10000);

        //this.database = this.client.GetDatabase("TestDatabase");
        this.container = containerResponse.Container;

        await this.CreateItemsAsync(0, 100); // Pre-create data for all operations

       // Console.WriteLine("Setup completed successfully." +containerResponse);
        }

    private async Task CreateItemsAsync(int start, int count)
    {
        for (int i = start; i < start + count; i++)
        {
            JObject newItem = new JObject
            {
                { "id", i.ToString() },
                { "name", $"Item_{i}" },
                { "otherdata", Guid.NewGuid().ToString() }
            };
            await this.container.UpsertItemAsync(newItem, new PartitionKey(i.ToString()));
        }
    }

    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        Console.WriteLine("Cleaning up resources...");
        await this.database.DeleteAsync();
        this.client.Dispose();
    }

    [Benchmark]
    public async Task CreateItemAsync()
    {
        // Ensure a unique ID to avoid conflicts
        string uniqueId = Guid.NewGuid().ToString();

        JObject newItem = new JObject
        {
            { "id", uniqueId },
            { "name", $"NewItem_{uniqueId}" }
        };

        await this.container.CreateItemAsync(newItem, new PartitionKey(uniqueId));
    }

    [Benchmark]
    public async Task CreateItemStreamAsync()
    {
        JObject newItem = new JObject
    {
        { "id", "102" },
        { "name", "NewItem_102" }
    };
        using Stream stream = ToStream(newItem);
        await this.container.CreateItemStreamAsync(stream, new PartitionKey("102"));
    }

    [Benchmark]
    public async Task ReadItemAsync()
    {
        await this.container.ReadItemAsync<JObject>("0", new PartitionKey("0"));
    }

    [Benchmark]
    public async Task ReadItemStreamAsync()
    {
        using ResponseMessage response = await this.container.ReadItemStreamAsync("1", new PartitionKey("1"));
        response.EnsureSuccessStatusCode();
    }

    [Benchmark]
    public async Task UpsertItemAsync()
    {
        JObject updatedItem = new JObject
    {
        { "id", "2" },
        { "name", "UpdatedItem_2" }
    };
        await this.container.UpsertItemAsync(updatedItem, new PartitionKey("2"));
    }

    [Benchmark]
    public async Task UpsertItemStreamAsync()
    {
        JObject updatedItem = new JObject
    {
        { "id", "3" },
        { "name", "UpdatedItem_3" }
    };
        using Stream stream = ToStream(updatedItem);
        await this.container.UpsertItemStreamAsync(stream, new PartitionKey("3"));
    }

    [Benchmark]
    public async Task ReplaceItemAsync()
    {
        JObject replacedItem = new JObject
    {
        { "id", "4" },
        { "name", "ReplacedItem_4" }
    };
        await this.container.ReplaceItemAsync(replacedItem, "4", new PartitionKey("4"));
    }

    [Benchmark]
    public async Task ReplaceItemStreamAsync()
    {
        JObject replacedItem = new JObject
    {
        { "id", "5" },
        { "name", "ReplacedItem_5" }
    };
        using Stream stream = ToStream(replacedItem);
        await this.container.ReplaceItemStreamAsync(stream, "5", new PartitionKey("5"));
    }

    [Benchmark]
    public async Task DeleteItemAsync()
    {
        await this.SafeDeleteAsync("6");
    }

    [Benchmark]
    public async Task DeleteItemStreamAsync()
    {
        await this.SafeDeleteStreamAsync("7");
    }

    private async Task SafeDeleteAsync(string id)
    {
        try
        {
            await this.container.DeleteItemAsync<JObject>(id, new PartitionKey(id));
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            Console.WriteLine($"Item {id} not found: Skipping deletion.");
        }
    }

    private async Task SafeDeleteStreamAsync(string id)
    {
        try
        {
            using ResponseMessage response = await this.container.DeleteItemStreamAsync(id, new PartitionKey(id));
            response.EnsureSuccessStatusCode();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            Console.WriteLine($"Item {id} not found: Skipping stream deletion.");
        }
    }

    private static Stream ToStream<T>(T input)
    {
        string json = JsonConvert.SerializeObject(input);
        return new MemoryStream(Encoding.UTF8.GetBytes(json));
    }

    private class CustomBenchmarkConfig : ManualConfig
    {
        public CustomBenchmarkConfig()
        {
            this.AddColumn(StatisticColumn.OperationsPerSecond);
            this.AddColumn(StatisticColumn.P95);

            // Minimal run to reduce time
            this.AddJob(Job.Default
                .WithLaunchCount(1)    // Single launch
                .WithWarmupCount(1)    // Just 1 warmup run
                .WithIterationCount(1) // Single measured iteration
                .WithStrategy(BenchmarkDotNet.Engines.RunStrategy.Throughput));

            this.AddExporter(HtmlExporter.Default);
            this.AddExporter(CsvExporter.Default);
        }
    }
  }
}