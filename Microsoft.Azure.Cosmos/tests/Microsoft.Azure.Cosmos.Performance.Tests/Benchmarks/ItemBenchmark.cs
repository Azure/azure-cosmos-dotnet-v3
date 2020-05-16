// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Benchmark for Item related operations.
    /// </summary>
    [MemoryDiagnoser]
    public class ItemBenchmark
    {
        private readonly CosmosClient clientForTests;
        private readonly Container container;
        private JObject baseItem;
        private byte[] payloadBytes;

        /// <summary>
        /// Initializes a new instance of the <see cref="ItemBenchmark"/> class.
        /// </summary>
        public ItemBenchmark()
        {
            this.clientForTests = MockDocumentClient.CreateMockCosmosClient();
            this.container = this.clientForTests.GetDatabase("myDB").GetContainer("myColl");
            this.baseItem = JObject.Parse(File.ReadAllText("samplepayload.json"));
            using (FileStream tmp = File.OpenRead("samplepayload.json"))
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    tmp.CopyTo(ms);
                    this.payloadBytes = ms.ToArray();
                }
            }
        }

        /// <summary>
        /// Benchmark for CreateItemAsync.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Benchmark]
        public async Task CreateItem()
        {
            using (MemoryStream ms = new MemoryStream(this.payloadBytes))
            {
                using (ResponseMessage response = await this.container.CreateItemStreamAsync(
                    ms,
                    new Cosmos.PartitionKey(Constants.ValidOperationId)))
                {
                    string diagnostics = response.Diagnostics.ToString();
                    if ((int)response.StatusCode > 300 || response.Content == null)
                    {
                        throw new Exception();
                    }
                }
            }
        }

        /// <summary>
        /// Benchmark for UpsertItemAsync.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Benchmark]
        public async Task UpsertItem()
        {
            using (ResponseMessage response = await this.container.UpsertItemStreamAsync(
                new MemoryStream(this.payloadBytes),
                new Cosmos.PartitionKey(Constants.ValidOperationId)))
            {
                string diagnostics = response.Diagnostics.ToString();
                if ((int)response.StatusCode > 300 || response.Content == null)
                {
                    throw new Exception();
                }
            }
        }

        /// <summary>
        /// Benchmark for UpsertItemAsync with Stream.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Benchmark]
        public async Task UpsertItemStream()
        {

            using (ResponseMessage response = await this.container.UpsertItemStreamAsync(
                    new MemoryStream(this.payloadBytes),
                    new Cosmos.PartitionKey(Constants.ValidOperationId)))
            {
                string diagnostics = response.Diagnostics.ToString();
                if ((int)response.StatusCode > 300 || response.Content.Length == 0)
                {
                    throw new Exception();
                }
            }
        }

        /// <summary>
        /// Benchmark for ReadItemAsync.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Benchmark]
        public async Task ReadItemNotExists()
        {
            using (ResponseMessage response = await this.container.ReadItemStreamAsync(
                Constants.NotFoundOperationId,
                new Cosmos.PartitionKey(Constants.ValidOperationId)))
            {
                string diagnostics = response.Diagnostics.ToString();
                if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    throw new Exception();
                }
            }
        }

        /// <summary>
        /// Benchmark for ReadItemStreamAsync.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Benchmark]
        public async Task ReadItemStream()
        {
            using (ResponseMessage response = await this.container.ReadItemStreamAsync(
                Constants.ValidOperationId,
                new Cosmos.PartitionKey(Constants.ValidOperationId)))
            {
                string diagnostics = response.Diagnostics.ToString();
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound || response.Content == null)
                {
                    throw new Exception();
                }
            }
        }

        /// <summary>
        /// Benchmark for ReplaceItemAsync.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Benchmark]
        public async Task UpdateItem()
        {
            using (ResponseMessage response = await this.container.ReplaceItemStreamAsync(
                new MemoryStream(this.payloadBytes),
                Constants.ValidOperationId,
                new Cosmos.PartitionKey(Constants.ValidOperationId)))
            {
                string diagnostics = response.Diagnostics.ToString();
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound || response.Content == null)
                {
                    throw new Exception();
                }
            }
        }

        /// <summary>
        /// Benchmark for DeleteItemAsync.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Benchmark]
        public async Task DeleteItem()
        {
            using (ResponseMessage response = await this.container.DeleteItemStreamAsync(
                Constants.ValidOperationId,
                new Cosmos.PartitionKey(Constants.ValidOperationId)))
            {
                string diagnostics = response.Diagnostics.ToString();
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    throw new Exception();
                }
            }
        }

        /// <summary>
        /// Benchmark for DeleteItemAsync.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Benchmark]
        public async Task DeleteItemNotExists()
        {
            using (ResponseMessage response = await this.container.DeleteItemStreamAsync(
                Constants.NotFoundOperationId,
                new Cosmos.PartitionKey(Constants.ValidOperationId)))
            {
                string diagnostics = response.Diagnostics.ToString();
                if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    throw new Exception();
                }
            }
        }

        /// <summary>
        /// Benchmark for DeleteItemAsync.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Benchmark]
        public async Task ReadFeedStream()
        {
            FeedIterator streamIterator = this.container.GetItemQueryStreamIterator();
            while(streamIterator.HasMoreResults)
            {
                ResponseMessage response = await streamIterator.ReadNextAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception();
                }
            }
        }
    }
}