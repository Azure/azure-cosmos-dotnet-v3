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
        private readonly CosmosContainer container;
        private JObject baseItem;
        private Stream baseStream;

        /// <summary>
        /// Initializes a new instance of the <see cref="ItemBenchmark"/> class.
        /// </summary>
        public ItemBenchmark()
        {
            this.clientForTests = MockDocumentClient.CreateMockCosmosClient();
            this.container = this.clientForTests.Databases["myDB"].Containers["myColl"];
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
                Constants.ValidOperationId,
                this.baseItem);
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
                Constants.ValidOperationId,
                this.baseItem);
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
        public async Task UpsertItemAsStream()
        {
            this.baseStream.Position = 0;
            var response = await this.container.UpsertItemAsStreamAsync(
                    Constants.ValidOperationId,
                    this.baseStream);
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
                Constants.ValidOperationId);
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
            var response = await this.container.ReadItemAsync<JObject>(
                Constants.ValidOperationId,
                Constants.NotFoundOperationId);
            if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                throw new Exception();
            }
        }

        /// <summary>
        /// Benchmark for ReadItemStreamAsync.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Benchmark]
        public async Task ReadItemAsStream()
        {
            var response = await this.container.ReadItemAsStreamAsync(
                Constants.ValidOperationId, 
                Constants.ValidOperationId);
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
                Constants.ValidOperationId,
                Constants.ValidOperationId, 
                this.baseItem);
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
                Constants.ValidOperationId);
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
            var response = await this.container.DeleteItemAsync<JObject>(
                Constants.ValidOperationId,
                Constants.NotFoundOperationId);
            if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                throw new Exception();
            }
        }
    }
}