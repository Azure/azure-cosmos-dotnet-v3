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
    using Microsoft.Azure.Cosmos.Json;

    [MemoryDiagnoser]
    [BenchmarkCategory("GateBenchmark")]
    [Config(typeof(BinaryEncodingEnabledBenchmark.CustomBenchmarkConfig))]
    public class BinaryEncodingEnabledBenchmark
    {
        private MockedItemBenchmarkHelper benchmarkHelper;
        private Container container;

        [Params(true)]
        public bool EnableBinaryResponseOnPointOperations;

        [GlobalSetup]
        public async Task GlobalSetupAsync()
        {
            // Set the environment variable to enable or disable binary encoding
            Environment.SetEnvironmentVariable("COSMOS_ENABLE_BINARY_ENCODING", this.EnableBinaryResponseOnPointOperations.ToString());

            // Initialize the mocked environment
            JsonSerializationFormat serializationFormat = this.EnableBinaryResponseOnPointOperations ? JsonSerializationFormat.Binary : JsonSerializationFormat.Text;
            this.benchmarkHelper = new MockedItemBenchmarkHelper(serializationFormat: serializationFormat);
            this.container = this.benchmarkHelper.TestContainer;

            // Create the item in the container
            using (MemoryStream ms = this.benchmarkHelper.GetItemPayloadAsStream())
            using (ResponseMessage response = await this.container.CreateItemStreamAsync(
                ms,
                new PartitionKey(MockedItemBenchmarkHelper.ExistingItemId)))
            {
                if ((int)response.StatusCode > 300 || response.Content == null)
                {
                    throw new InvalidOperationException($"Failed to create item with status code {response.StatusCode}");
                }
            }
        }

        [Benchmark]
        public async Task CreateItemAsync()
        {
            ItemRequestOptions requestOptions = new ItemRequestOptions
            {
                EnableBinaryResponseOnPointOperations = this.EnableBinaryResponseOnPointOperations,
            };

            ItemResponse<ToDoActivity> itemResponse = await this.container.CreateItemAsync(
                item: this.benchmarkHelper.TestItem,
                partitionKey: new PartitionKey(MockedItemBenchmarkHelper.ExistingItemId),
                requestOptions: requestOptions);

            if (itemResponse.StatusCode != HttpStatusCode.Created && itemResponse.StatusCode != HttpStatusCode.OK)
            {
                throw new InvalidOperationException($"Item {this.benchmarkHelper.TestItem.id} was not created.");
            }
        }

        [Benchmark]
        public async Task CreateItemStreamAsync()
        {
            ItemRequestOptions requestOptions = new ItemRequestOptions
            {
                EnableBinaryResponseOnPointOperations = this.EnableBinaryResponseOnPointOperations,
            };

            using (MemoryStream ms = this.benchmarkHelper.GetItemPayloadAsStream())
            using (ResponseMessage response = await this.container.CreateItemStreamAsync(
                ms,
                new PartitionKey(MockedItemBenchmarkHelper.ExistingItemId),
                requestOptions))
            {
                if ((int)response.StatusCode > 300 || response.Content == null)
                {
                    throw new InvalidOperationException($"Item {this.benchmarkHelper.TestItem.id} was not created stream.");
                }
            }
        }

        [Benchmark]
        public async Task ReadItemAsync()
        {
            ItemRequestOptions requestOptions = new ItemRequestOptions
            {
                EnableBinaryResponseOnPointOperations = this.EnableBinaryResponseOnPointOperations,
            };

            ItemResponse<ToDoActivity> itemResponse = await this.container.ReadItemAsync<ToDoActivity>(
                id: MockedItemBenchmarkHelper.ExistingItemId,
                partitionKey: new PartitionKey(MockedItemBenchmarkHelper.ExistingItemId),
                requestOptions: requestOptions);

            if (itemResponse.StatusCode != HttpStatusCode.OK)
            {
                throw new InvalidOperationException($"Item {MockedItemBenchmarkHelper.ExistingItemId} was not read.");
            }
        }

        [Benchmark]
        public async Task ReadItemStreamAsync()
        {
            ItemRequestOptions requestOptions = new ItemRequestOptions
            {
                EnableBinaryResponseOnPointOperations = this.EnableBinaryResponseOnPointOperations,
            };

            ResponseMessage response = await this.container.ReadItemStreamAsync(
                id: MockedItemBenchmarkHelper.ExistingItemId,
                partitionKey: new PartitionKey(MockedItemBenchmarkHelper.ExistingItemId),
                requestOptions: requestOptions);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new InvalidOperationException($"Item {MockedItemBenchmarkHelper.ExistingItemId} was not read stream.");
            }
        }

        [Benchmark]
        public async Task UpsertItemAsync()
        {
            ItemRequestOptions requestOptions = new ItemRequestOptions
            {
                EnableBinaryResponseOnPointOperations = this.EnableBinaryResponseOnPointOperations,
            };

            ItemResponse<ToDoActivity> itemResponse = await this.container.UpsertItemAsync(
                item: this.benchmarkHelper.TestItem,
                partitionKey: new PartitionKey(MockedItemBenchmarkHelper.ExistingItemId),
                requestOptions: requestOptions);

            if (itemResponse.StatusCode != HttpStatusCode.OK)
            {
                throw new InvalidOperationException($"Item {this.benchmarkHelper.TestItem.id} was not upserted.");
            }
        }

        [Benchmark]
        public async Task UpsertItemStreamAsync()
        {
            ItemRequestOptions requestOptions = new ItemRequestOptions
            {
                EnableBinaryResponseOnPointOperations = this.EnableBinaryResponseOnPointOperations,
            };

            using (MemoryStream ms = this.benchmarkHelper.GetItemPayloadAsStream())
            using (ResponseMessage response = await this.container.UpsertItemStreamAsync(
                ms,
                new PartitionKey(MockedItemBenchmarkHelper.ExistingItemId),
                requestOptions))
            {
                if ((int)response.StatusCode > 300 || response.Content == null)
                {
                    throw new InvalidOperationException($"Item {this.benchmarkHelper.TestItem.id} was not upserted stream.");
                }
            }
        }

        [Benchmark]
        public async Task ReplaceItemAsync()
        {
            ItemRequestOptions requestOptions = new ItemRequestOptions
            {
                EnableBinaryResponseOnPointOperations = this.EnableBinaryResponseOnPointOperations,
            };

            ItemResponse<ToDoActivity> itemResponse = await this.container.ReplaceItemAsync(
                item: this.benchmarkHelper.TestItem,
                id: MockedItemBenchmarkHelper.ExistingItemId,
                partitionKey: new PartitionKey(MockedItemBenchmarkHelper.ExistingItemId),
                requestOptions: requestOptions);

            if (itemResponse.StatusCode != HttpStatusCode.OK)
            {
                throw new InvalidOperationException($"Item {this.benchmarkHelper.TestItem.id} was not replaced.");
            }
        }

        [Benchmark]
        public async Task ReplaceItemStreamAsync()
        {
            ItemRequestOptions requestOptions = new ItemRequestOptions
            {
                EnableBinaryResponseOnPointOperations = this.EnableBinaryResponseOnPointOperations,
            };

            using (MemoryStream ms = this.benchmarkHelper.GetItemPayloadAsStream())
            using (ResponseMessage response = await this.container.ReplaceItemStreamAsync(
                ms,
                MockedItemBenchmarkHelper.ExistingItemId,
                new PartitionKey(MockedItemBenchmarkHelper.ExistingItemId),
                requestOptions))
            {
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new InvalidOperationException($"Item {this.benchmarkHelper.TestItem.id} was not replaced stream.");
                }
            }
        }

        [Benchmark]
        public async Task DeleteItemAsync()
        {
            ItemRequestOptions requestOptions = new ItemRequestOptions
            {
                EnableBinaryResponseOnPointOperations = this.EnableBinaryResponseOnPointOperations,
            };

            ItemResponse<ToDoActivity> itemResponse = await this.container.DeleteItemAsync<ToDoActivity>(
                id: MockedItemBenchmarkHelper.ExistingItemId,
                partitionKey: new PartitionKey(MockedItemBenchmarkHelper.ExistingItemId),
                requestOptions: requestOptions);

            if (itemResponse.StatusCode != HttpStatusCode.OK)
            {
                throw new InvalidOperationException($"Item {MockedItemBenchmarkHelper.ExistingItemId} was not deleted.");
            }
        }

        [Benchmark]
        public async Task DeleteItemStreamAsync()
        {
            ItemRequestOptions requestOptions = new ItemRequestOptions
            {
                EnableBinaryResponseOnPointOperations = this.EnableBinaryResponseOnPointOperations,
            };

            ResponseMessage response = await this.container.DeleteItemStreamAsync(
                id: MockedItemBenchmarkHelper.ExistingItemId,
                partitionKey: new PartitionKey(MockedItemBenchmarkHelper.ExistingItemId),
                requestOptions: requestOptions);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new InvalidOperationException($"Item {MockedItemBenchmarkHelper.ExistingItemId} was not deleted stream.");
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            // Restore the environment variable to its original value
            Environment.SetEnvironmentVariable("COSMOS_ENABLE_BINARY_ENCODING", "false");
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

                this.AddDiagnoser(MemoryDiagnoser.Default);
                this.AddDiagnoser(ThreadingDiagnoser.Default);
                this.AddColumnProvider(DefaultConfig.Instance.GetColumnProviders().ToArray());

                // Minimal run to reduce time
                this.AddJob(Job.ShortRun
                    .WithStrategy(BenchmarkDotNet.Engines.RunStrategy.Throughput));

                this.AddExporter(HtmlExporter.Default);
                this.AddExporter(CsvExporter.Default);
            }
        }
    }
}