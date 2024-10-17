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
    using BenchmarkDotNet.Diagnosers;

    [MemoryDiagnoser]
    [BenchmarkCategory("NewGateBenchmark")]
    [Config(typeof(CustomBenchmarkConfig))]
    public class BinaryEncodingBenchmark
    {
        private readonly string binaryConnectionString = "";
        private readonly string databaseName = "MyBinaryTestDatabase";
        private readonly string containerName = "MyBinaryBenchmarkContainer";
        private readonly string partitionKeyPath = "/pk";
        private readonly List<Comment> readItems = new();
        private readonly List<Comment> upsertItems = new();
        private readonly List<Comment> replaceItems = new();
        private readonly List<Comment> deleteItems = new();
        private readonly List<Comment> deleteStreamItems = new();
        private readonly Random random = new();
        private CosmosClient client;
        private Database database;
        private Container container;

        [GlobalSetup(Targets = new[] { nameof(CreateItemAsync), nameof(CreateItemStreamAsync) })]
        public async Task GlobalSetupCreate()
        {
            await this.InitializeDatabaseAndContainers();
        }

        [GlobalSetup(Targets = new[] { nameof(ReadItemAsync), nameof(ReadItemStreamAsync) })]
        public async Task GlobalSetupRead()
        {
            await this.InitializeDatabaseAndContainers();
            await this.InitializeContainerWithPreCreatedItemsAsync(0, 100, this.readItems); // Pre-create data for read operations
            Console.WriteLine("Inserted documents for read benchmark.");
        }

        [GlobalSetup(Targets = new[] { nameof(UpsertItemAsync), nameof(UpsertItemStreamAsync) })]
        public async Task GlobalSetupUpsert()
        {
            await this.InitializeDatabaseAndContainers();
            await this.InitializeContainerWithPreCreatedItemsAsync(0, 100, this.upsertItems); // Pre-create data for upsert operations
            Console.WriteLine("Inserted documents for upsert benchmark.");
        }

        [GlobalSetup(Targets = new[] { nameof(ReplaceItemAsync), nameof(ReplaceItemStreamAsync) })]
        public async Task GlobalSetupReplace()
        {
            await this.InitializeDatabaseAndContainers();
            await this.InitializeContainerWithPreCreatedItemsAsync(0, 100, this.replaceItems); // Pre-create data for replace operations
            Console.WriteLine("Inserted documents for replace benchmark.");
        }

        [GlobalSetup(Targets = new[] { nameof(DeleteItemAsync) })]
        public async Task GlobalSetupDelete()
        {
            await this.InitializeDatabaseAndContainers();
            await this.InitializeContainerWithPreCreatedItemsAsync(0, 100, this.deleteItems); // Pre-create data for delete operations
            Console.WriteLine("Inserted documents for delete benchmark.");
        }

        [GlobalSetup(Targets = new[] { nameof(DeleteItemStreamAsync) })]
        public async Task GlobalSetupDeleteStream()
        {
            await this.InitializeDatabaseAndContainers();
            await this.InitializeContainerWithPreCreatedItemsAsync(0, 100, this.deleteStreamItems); // Pre-create data for delete stream operations
            Console.WriteLine("Inserted documents for delete stream benchmark.");
        }

        private async Task InitializeDatabaseAndContainers()
        {
            Console.WriteLine("Creating Database and Containers.");

            CosmosClientOptions clientOptions = new CosmosClientOptions
            {
                ApplicationName = "dkunda-binary-encoding-perf-app",
                EnableContentResponseOnWrite = true,
                ApplicationPreferredRegions = new List<string> { Regions.WestEurope },
                RequestTimeout = TimeSpan.FromSeconds(10),
                ConsistencyLevel = ConsistencyLevel.Strong,
                //UseSystemTextJsonSerializerWithOptions = new System.Text.Json.JsonSerializerOptions()
                //{
                //}
            };

            this.client = new CosmosClient(this.binaryConnectionString, clientOptions);
            this.database = await this.client.CreateDatabaseIfNotExistsAsync(this.databaseName);

            ContainerResponse containerResponse = await this.database.CreateContainerIfNotExistsAsync(
                id: this.containerName,
                partitionKeyPath: this.partitionKeyPath,
                throughput: 10000);

            this.database = this.client.GetDatabase(this.databaseName);
            this.container = containerResponse.Container;

            Console.WriteLine("Successfully created Database and Containers with status: " + containerResponse.StatusCode);
        }

        private async Task InitializeContainerWithPreCreatedItemsAsync(int start, int count, List<Comment> items)
        {
            for (int i = start; i < count; i++)
            {
                Comment comment = this.GetRandomCommentItem();

                ItemRequestOptions requestOptions = new ItemRequestOptions
                {
                    //EnableBinaryResponseOnPointOperations = true,
                };

                ItemResponse<Comment> writeResponse = await this.container.CreateItemAsync<Comment>(
                    item: comment,
                    partitionKey: new PartitionKey(comment.pk),
                    requestOptions: requestOptions
                );

                if (writeResponse.StatusCode == HttpStatusCode.Created)
                {
                    items.Add(comment);
                }
            }
        }

        [GlobalCleanup]
        public async Task CleanupAsync()
        {
            Console.WriteLine("Cleaning up resources...");
            await this.container.DeleteContainerAsync();
            await this.database.DeleteAsync();
            this.client.Dispose();
        }

        [Benchmark]
        public async Task CreateItemAsync()
        {
            Comment comment = this.GetRandomCommentItem();

            ItemRequestOptions requestOptions = new ItemRequestOptions
            {
                EnableBinaryResponseOnPointOperations = false,
            };

            ItemResponse<Comment> itemResponse = await this.container.CreateItemAsync<Comment>(
                item: comment,
                partitionKey: new PartitionKey(comment.pk),
                requestOptions: requestOptions
            );

            if (itemResponse.StatusCode != HttpStatusCode.Created)
            {
                Console.WriteLine($"Error: Item {comment.id} was not created.");
            }
        }

        [Benchmark]
        public async Task CreateItemStreamAsync()
        {
            Comment comment = this.GetRandomCommentItem();

            using Stream stream = ToStream(comment);
            ResponseMessage itemResponse = await this.container.CreateItemStreamAsync(stream, new PartitionKey(comment.pk));

            if (itemResponse.StatusCode != HttpStatusCode.Created)
            {
                Console.WriteLine($"Error: Item {comment.id} was not created stream.");
            }
        }

        [Benchmark]
        public async Task ReadItemAsync()
        {
            int index = this.random.Next(this.readItems.Count);

            Comment comment = this.readItems[index];

            ItemRequestOptions requestOptions = new ItemRequestOptions
            {
                EnableBinaryResponseOnPointOperations = false,
            };

            ItemResponse<Comment> itemResponse = await this.container.ReadItemAsync<Comment>(
                id: comment.id,
                partitionKey: new PartitionKey(comment.pk),
                requestOptions: requestOptions
                );

            if (itemResponse.StatusCode != HttpStatusCode.OK)
            {
                Console.WriteLine($"Error: Item {comment.id} was not read.");
            }
        }

        [Benchmark]
        public async Task ReadItemStreamAsync()
        {
            int index = this.random.Next(this.readItems.Count);

            Comment comment = this.readItems[index];

            ItemRequestOptions requestOptions = new ItemRequestOptions
            {
                EnableBinaryResponseOnPointOperations = false,
            };

            ResponseMessage itemResponse = await this.container.ReadItemStreamAsync(
                id: comment.id,
                partitionKey: new PartitionKey(comment.pk),
                requestOptions: requestOptions
                );

            if (itemResponse.StatusCode != HttpStatusCode.OK)
            {
                Console.WriteLine($"Error: Item {comment.id} was not read stream.");
            }
        }

        [Benchmark]
        public async Task UpsertItemAsync()
        {
            int index = this.random.Next(this.upsertItems.Count);

            Comment comment = this.upsertItems[index];
            comment.Name = "UpdatedName";

            ItemRequestOptions requestOptions = new ItemRequestOptions
            {
                EnableBinaryResponseOnPointOperations = false,
            };

            ItemResponse<Comment> itemResponse = await this.container.UpsertItemAsync<Comment>(
                item: comment,
                partitionKey: new PartitionKey(comment.pk),
                requestOptions: requestOptions
                );

            if (itemResponse.StatusCode != HttpStatusCode.OK)
            {
                Console.WriteLine($"Error: Item {comment.id} was not upserted.");
            }
        }

        [Benchmark]
        public async Task UpsertItemStreamAsync()
        {
            int index = this.random.Next(this.upsertItems.Count);

            Comment comment = this.upsertItems[index];
            comment.Name = "UpdatedNameStream";

            using Stream stream = ToStream(comment);

            ResponseMessage itemResponse = await this.container.UpsertItemStreamAsync(stream, new PartitionKey(comment.pk));

            if (itemResponse.StatusCode != HttpStatusCode.OK)
            {
                Console.WriteLine($"Error: Item {comment.id} was not upserted stream.");
            }
        }

        [Benchmark]
        public async Task ReplaceItemAsync()
        {
            int index = this.random.Next(this.replaceItems.Count);

            Comment comment = this.replaceItems[index];
            comment.Name = "ReplacedName";

            ItemResponse<Comment> itemResponse = await this.container.ReplaceItemAsync(comment, comment.id, new PartitionKey(comment.pk));

            if (itemResponse.StatusCode != HttpStatusCode.OK)
            {
                Console.WriteLine($"Error: Item {comment.id} was not replaced.");
            }
        }

        [Benchmark]
        public async Task ReplaceItemStreamAsync()
        {
            int index = this.random.Next(this.replaceItems.Count);

            Comment comment = this.replaceItems[index];
            comment.Name = "ReplacedNameStream";

            using Stream stream = ToStream(comment);
            ResponseMessage itemResponse = await this.container.ReplaceItemStreamAsync(stream, comment.id, new PartitionKey(comment.pk));

            if (itemResponse.StatusCode != HttpStatusCode.OK)
            {
                Console.WriteLine($"Error: Item {comment.id} was not replaced stream.");
            }
        }

        [Benchmark]
        public async Task DeleteItemAsync()
        {
            int index = this.random.Next(this.deleteItems.Count);
            if (index >= 0 && index < this.deleteItems.Count)
            {
                Comment comment = this.deleteItems[index];
                this.deleteItems.Remove(comment);

                ItemResponse<Comment> itemResponse = await this.container.DeleteItemAsync<Comment>(comment.id, new PartitionKey(comment.pk));

                if (itemResponse.StatusCode == HttpStatusCode.NotFound)
                {
                    Console.WriteLine($"Error: Item {comment.id} was not deleted : " + itemResponse.StatusCode);
                }
            }
        }

        [Benchmark]
        public async Task DeleteItemStreamAsync()
        {
            int index = this.random.Next(this.deleteStreamItems.Count);
            if (index >= 0 && index < this.deleteItems.Count)
            {
                Comment comment = this.deleteStreamItems[index];
                this.deleteStreamItems.Remove(comment);

                ResponseMessage itemResponse = await this.container.DeleteItemStreamAsync(comment.id, new PartitionKey(comment.pk));

                if (itemResponse.StatusCode == HttpStatusCode.NotFound)
                {
                    Console.WriteLine($"Error: Item {comment.id} was not deleted stream: " + itemResponse.StatusCode);
                }
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
                this.AddColumn(StatisticColumn.Q3);
                this.AddColumn(StatisticColumn.P80);
                this.AddColumn(StatisticColumn.P85);
                this.AddColumn(StatisticColumn.P90);
                this.AddColumn(StatisticColumn.P95);
                this.AddColumn(StatisticColumn.P100);

                this.AddDiagnoser(new IDiagnoser[] { MemoryDiagnoser.Default, ThreadingDiagnoser.Default });
                this.AddColumnProvider(DefaultConfig.Instance.GetColumnProviders().ToArray());

                // Minimal run to reduce time
                this.AddJob(Job.ShortRun
                    .WithStrategy(BenchmarkDotNet.Engines.RunStrategy.Throughput));

                this.AddExporter(HtmlExporter.Default);
                this.AddExporter(CsvExporter.Default);
            }
        }

        public class Comment {
            public string id;
            public string pk;
            public string Name;
            public string Email;
            public string Body;

            public Comment(
                string id,
                string pk,
                string name,
                string email,
                string body)
            {
                this.id = id;
                this.pk = pk;
                this.Name = name;
                this.Email = email;
                this.Body = body;
            }
        }

        private Comment GetRandomCommentItem()
        {
            return new(
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString(),
                this.random.Next().ToString(),
                "dkunda@test.com",
                "This document is intended for binary encoding perf testing.");
        }
    }
}