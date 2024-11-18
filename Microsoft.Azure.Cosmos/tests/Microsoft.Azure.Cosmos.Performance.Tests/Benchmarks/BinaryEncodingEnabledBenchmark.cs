namespace Microsoft.Azure.Cosmos.Benchmarks
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Columns;
    using BenchmarkDotNet.Configs;
    using BenchmarkDotNet.Diagnosers;
    using BenchmarkDotNet.Exporters.Csv;
    using BenchmarkDotNet.Exporters;
    using BenchmarkDotNet.Jobs;
    using Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks;
    using Newtonsoft.Json;

    [MemoryDiagnoser]
    [BenchmarkCategory("NewGateBenchmark")]
    [Config(typeof(CustomBenchmarkConfig))]
    public class BinaryEncodingEnabledBenchmark
    {
        private readonly MockedItemBenchmarkHelper benchmarkHelper;
        private readonly Container container;
        private readonly List<Comment> readItems = new();
        private readonly List<Comment> upsertItems = new();
        private readonly List<Comment> replaceItems = new();
        private readonly List<Comment> deleteItems = new();
        private readonly List<Comment> deleteStreamItems = new();
        private readonly Random random = new();

        public BinaryEncodingEnabledBenchmark()
        {
            Environment.SetEnvironmentVariable("AZURE_COSMOS_BINARY_ENCODING_ENABLED", "True");
            // Initialize the mocked environment
            this.benchmarkHelper = new MockedItemBenchmarkHelper();
            this.container = this.benchmarkHelper.TestContainer;

            // Prepopulate test data
            this.InitializeContainerWithPreCreatedItems(0, 100, this.readItems);
            this.InitializeContainerWithPreCreatedItems(0, 100, this.upsertItems);
            this.InitializeContainerWithPreCreatedItems(0, 100, this.replaceItems);
            this.InitializeContainerWithPreCreatedItems(0, 100, this.deleteItems);
            this.InitializeContainerWithPreCreatedItems(0, 100, this.deleteStreamItems);
        }

        private void InitializeContainerWithPreCreatedItems(int start, int count, List<Comment> items)
        {
            for (int i = start; i < count; i++)
            {
                Comment comment = this.GetRandomCommentItem();

                using Stream stream = ToStream(comment);
                ResponseMessage response = this.container.CreateItemStreamAsync(stream, new PartitionKey(comment.pk)).GetAwaiter().GetResult();

                if (response.StatusCode == HttpStatusCode.Created)
                {
                    items.Add(comment);
                }
            }
        }

        [GlobalCleanup]
        public async Task CleanupAsync()
        {
            await Task.CompletedTask;
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
            if (index >= 0 && index < this.deleteStreamItems.Count)
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

        private Comment GetRandomCommentItem()
        {
            return new Comment
            {
                id = Guid.NewGuid().ToString(),
                pk = Guid.NewGuid().ToString(),
                Name = "RandomName",
                Email = "test@example.com",
                Body = "This document is intended for binary encoding perf testing."
            };
        }

        private class CustomBenchmarkConfig : ManualConfig
        {
            public CustomBenchmarkConfig()
            {
                this.AddColumn(StatisticColumn.OperationsPerSecond);
                this.AddColumn(StatisticColumn.P95);
                this.AddDiagnoser(MemoryDiagnoser.Default);
                this.AddJob(Job.ShortRun.WithStrategy(BenchmarkDotNet.Engines.RunStrategy.Throughput));
            }
        }

        public class Comment
        {
            public string id;
            public string pk;
            public string Name;
            public string Email;
            public string Body;
        }
    }
}
