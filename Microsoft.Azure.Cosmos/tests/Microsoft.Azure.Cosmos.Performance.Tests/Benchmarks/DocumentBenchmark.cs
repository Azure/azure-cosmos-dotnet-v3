// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Performance.Tests.BenchmarkStrategies;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Benchmark for Item related operations.
    /// </summary>
    [MemoryDiagnoser]
    [NetThroughput(new BenchmarkFrameworks[] { BenchmarkFrameworks.NetFx471, BenchmarkFrameworks.NetCore21 }, maxIterations: 50)]
    public class ItemBenchmark
    {
        private readonly CosmosClient clientForTests;
        private readonly Container container;
        private JObject baseItem;
        private Stream baseStream;

        /// <summary>
        /// Initializes a new instance of the <see cref="ItemBenchmark"/> class.
        /// </summary>
        public ItemBenchmark()
        {
            this.clientForTests = MockDocumentClient.CreateMockCosmosClient();
            this.container = this.clientForTests.GetDatabase("myDB").GetContainer("myColl");
            this.baseItem = JObject.Parse(File.ReadAllText("samplepayload.json"));
            this.baseStream = File.OpenRead("samplepayload.json");
        }

        /// <summary>
        /// Benchmark for CreateItemAsync.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Benchmark]
        public async Task InsertItem()
        {
            var response = await this.container.CreateItemAsync(
                this.baseItem,
                new Cosmos.PartitionKey(Constants.ValidOperationId));
            if ((int)response.StatusCode > 300 || response.Resource == null)
            {
                throw new Exception();
            }
        }

        /// <summary>
        /// Benchmark for UpsertItemAsync.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Benchmark]
        public async Task UpsertItem()
        {
            var response = await this.container.UpsertItemAsync(
                this.baseItem,
                new Cosmos.PartitionKey(Constants.ValidOperationId));
            if ((int)response.StatusCode > 300 || response.Resource == null)
            {
                throw new Exception();
            }
        }

        /// <summary>
        /// Benchmark for UpsertItemAsync with Stream.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Benchmark]
        public async Task UpsertItemStream()
        {
            this.baseStream.Position = 0;
            var response = await this.container.UpsertItemStreamAsync(
                    this.baseStream,
                    new Cosmos.PartitionKey(Constants.ValidOperationId));
            if ((int)response.StatusCode > 300 || response.Content.Length == 0)
            {
                throw new Exception();
            }
        }

        /// <summary>
        /// Benchmark for ReadItemAsync.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Benchmark]
        public async Task ReadItem()
        {
            var response = await this.container.ReadItemAsync<JObject>(
                Constants.ValidOperationId,
                new Cosmos.PartitionKey(Constants.ValidOperationId));
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound || response.Resource == null)
            {
                throw new Exception();
            }
        }

        /// <summary>
        /// Benchmark for ReadItemAsync.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Benchmark]
        public async Task ReadItemNotExists()
        {
            try
            {
                var response = await this.container.ReadItemAsync<JObject>(
                    Constants.NotFoundOperationId,
                    new Cosmos.PartitionKey(Constants.ValidOperationId));
            }
            catch(CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
            }
        }

        /// <summary>
        /// Benchmark for ReadItemStreamAsync.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Benchmark]
        public async Task ReadItemStream()
        {
            var response = await this.container.ReadItemStreamAsync(
                Constants.ValidOperationId,
                new Cosmos.PartitionKey(Constants.ValidOperationId));
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound || response.Content == null)
            {
                throw new Exception();
            }
        }

        /// <summary>
        /// Benchmark for ReplaceItemAsync.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Benchmark]
        public async Task UpdateItem()
        {
            var response = await this.container.ReplaceItemAsync(
                this.baseItem,
                Constants.ValidOperationId, 
                new Cosmos.PartitionKey(Constants.ValidOperationId));
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound || response.Resource == null)
            {
                throw new Exception();
            }
        }

        /// <summary>
        /// Benchmark for DeleteItemAsync.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Benchmark]
        public async Task DeleteItem()
        {
            var response = await this.container.DeleteItemAsync<JObject>(
                Constants.ValidOperationId,
                new Cosmos.PartitionKey(Constants.ValidOperationId));
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new Exception();
            }
        }

        /// <summary>
        /// Benchmark for DeleteItemAsync.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Benchmark]
        public async Task DeleteItemNotExists()
        {
            try
            {
                var response = await this.container.DeleteItemAsync<JObject>(
                    Constants.NotFoundOperationId,
                    new Cosmos.PartitionKey(Constants.ValidOperationId));
            }
            catch(CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
            }
        }
    }
}