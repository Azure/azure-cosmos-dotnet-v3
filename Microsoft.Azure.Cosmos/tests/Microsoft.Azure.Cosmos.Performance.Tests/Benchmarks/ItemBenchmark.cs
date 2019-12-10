// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Documents.Collections;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Benchmark for Item related operations.
    /// </summary>
    [MemoryDiagnoser]
    public class ItemBenchmark
    {
        private static readonly Encoding DefaultEncoding = new UTF8Encoding(false, true);
        private readonly CosmosClient clientForTests;
        private readonly Container container;
        private readonly JsonSerializer jsonSerializer = new JsonSerializer();
        private JObject baseItem;
        private byte[] payloadBytes;
        private dynamic TestItem;
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

            this.TestItem = new
            {
                id = "test",
                pk = "what",
                value = 1245,
                stop = true,
            };
        }

        [Benchmark]
        public void SingletonJsonSerializeItem()
        {
            using (MemoryStream streamPayload = new MemoryStream())
            {
                using (StreamWriter streamWriter = new StreamWriter(streamPayload, encoding: ItemBenchmark.DefaultEncoding, bufferSize: 1024, leaveOpen: true))
                {
                    using (JsonWriter writer = new JsonTextWriter(streamWriter))
                    {
                        writer.Formatting = Newtonsoft.Json.Formatting.None;
                        this.jsonSerializer.Serialize(writer, this.TestItem);
                        writer.Flush();
                        streamWriter.Flush();
                    }
                }
            }
        }

        [Benchmark]
        public void InstanceJsonSerializeItem()
        {
            using (MemoryStream streamPayload = new MemoryStream())
            {
                using (StreamWriter streamWriter = new StreamWriter(streamPayload, encoding: ItemBenchmark.DefaultEncoding, bufferSize: 1024, leaveOpen: true))
                {
                    using (JsonWriter writer = new JsonTextWriter(streamWriter))
                    {
                        writer.Formatting = Newtonsoft.Json.Formatting.None;
                        JsonSerializer jsonSerializer = new JsonSerializer();
                        jsonSerializer.Serialize(writer, this.TestItem);
                        writer.Flush();
                        streamWriter.Flush();
                    }
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
            ResponseMessage response = await this.container.UpsertItemStreamAsync(
                new MemoryStream(this.payloadBytes),
                new Cosmos.PartitionKey(Constants.ValidOperationId));
            if ((int)response.StatusCode > 300 || response.Content == null)
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

            ResponseMessage response = await this.container.UpsertItemStreamAsync(
                    new MemoryStream(this.payloadBytes),
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
        public async Task ReadItemNotExists()
        {
            ResponseMessage response = await this.container.ReadItemStreamAsync(
                Constants.NotFoundOperationId,
                new Cosmos.PartitionKey(Constants.ValidOperationId));
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
        public async Task ReadItemStream()
        {
            ResponseMessage response = await this.container.ReadItemStreamAsync(
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
            ResponseMessage response = await this.container.ReplaceItemStreamAsync(
                new MemoryStream(this.payloadBytes),
                Constants.ValidOperationId,
                new Cosmos.PartitionKey(Constants.ValidOperationId));
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound || response.Content == null)
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
            ResponseMessage response = await this.container.DeleteItemStreamAsync(
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
            ResponseMessage response = await this.container.DeleteItemStreamAsync(
                Constants.NotFoundOperationId,
                new Cosmos.PartitionKey(Constants.ValidOperationId));
            if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                throw new Exception();
            }
        }
    }
}