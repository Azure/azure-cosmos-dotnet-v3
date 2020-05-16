// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using static Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks.ItemBenchmarkHelper;

    public interface IItemBenchmark
    {
        public Task CreateItem();

        public Task UpsertItem();

        public Task ReadItemNotExists();

        public Task ReadItemExists();

        public Task UpdateItem();

        public Task DeleteItemExists();

        public Task DeleteItemNotExists();

        public Task ReadFeedStream();
    }

    public class ItemStreamBenchmark : IItemBenchmark
    {
        private ItemBenchmarkHelper benchmarkHelper;

        public ItemStreamBenchmark()
        {
            this.benchmarkHelper = new ItemBenchmarkHelper();
        }

        /// <summary>
        /// Benchmark for CreateItemAsync.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Benchmark]
        public async Task CreateItem()
        {
            using (MemoryStream ms = new MemoryStream(this.benchmarkHelper.payloadBytes))
            {
                using (ResponseMessage response = await this.benchmarkHelper.container.CreateItemStreamAsync(
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
            using (ResponseMessage response = await this.benchmarkHelper.container.UpsertItemStreamAsync(
                new MemoryStream(this.benchmarkHelper.payloadBytes),
                new Cosmos.PartitionKey(Constants.ValidOperationId)))
            {
                if ((int)response.StatusCode > 300 || response.Content == null)
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
            using (ResponseMessage response = await this.benchmarkHelper.container.ReadItemStreamAsync(
                Constants.NotFoundOperationId,
                new Cosmos.PartitionKey(Constants.ValidOperationId)))
            {
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
        public async Task ReadItemExists()
        {
            using (ResponseMessage response = await this.benchmarkHelper.container.ReadItemStreamAsync(
                Constants.ValidOperationId,
                new Cosmos.PartitionKey(Constants.ValidOperationId)))
            {
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
            using (ResponseMessage response = await this.benchmarkHelper.container.ReplaceItemStreamAsync(
                new MemoryStream(this.benchmarkHelper.payloadBytes),
                Constants.ValidOperationId,
                new Cosmos.PartitionKey(Constants.ValidOperationId)))
            {
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
        public async Task DeleteItemExists()
        {
            using (ResponseMessage response = await this.benchmarkHelper.container.DeleteItemStreamAsync(
                Constants.ValidOperationId,
                new Cosmos.PartitionKey(Constants.ValidOperationId)))
            {
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
            using (ResponseMessage response = await this.benchmarkHelper.container.DeleteItemStreamAsync(
                Constants.NotFoundOperationId,
                new Cosmos.PartitionKey(Constants.ValidOperationId)))
            {
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
            FeedIterator streamIterator = this.benchmarkHelper.container.GetItemQueryStreamIterator();
            while (streamIterator.HasMoreResults)
            {
                ResponseMessage response = await streamIterator.ReadNextAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception();
                }
            }
        }
    }

    public enum ScenarioType
    {
        Stream      = 0,
        OfT         = 1,
        OfTCustom   = 2,
    }

    [MemoryDiagnoser]
    public class ItemBenchmark : IItemBenchmark
    {
        public static readonly IItemBenchmark[] IterParameters = new IItemBenchmark[]
            {
                new ItemStreamBenchmark(),
                new ItemOfTBenchmark() { BenchmarkHelper = new ItemBenchmarkHelper() },
                new ItemOfTBenchmark() { BenchmarkHelper = new ItemBenchmarkHelper(true) },
            };

        [Params(ScenarioType.Stream, ScenarioType.OfT, ScenarioType.OfTCustom)]
        public ScenarioType Type
        {
            get;
            set;
        }

        private IItemBenchmark CurrentBenchmark
        {
            get
            {
                return ItemBenchmark.IterParameters[(int)this.Type];
            }
        }

        [Benchmark]
        public async Task CreateItem()
        {
            await this.CurrentBenchmark.CreateItem();
        }

        [Benchmark]
        public async Task DeleteItemExists()
        {
            await this.CurrentBenchmark.DeleteItemExists();
        }

        [Benchmark]
        public async Task DeleteItemNotExists()
        {
            await this.CurrentBenchmark.DeleteItemNotExists();
        }

        [Benchmark]
        public async Task ReadFeedStream()
        {
            await this.CurrentBenchmark.ReadFeedStream();
        }

        [Benchmark]
        public async Task ReadItemExists()
        {
            await this.CurrentBenchmark.ReadItemExists();
        }

        [Benchmark]
        public async Task ReadItemNotExists()
        {
            await this.CurrentBenchmark.ReadItemNotExists();
        }

        [Benchmark]
        public async Task UpdateItem()
        {
            await this.CurrentBenchmark.UpdateItem();
        }

        [Benchmark]
        public async Task UpsertItem()
        {
            await this.CurrentBenchmark.UpsertItem();
        }
    }


    public class ItemOfTBenchmark : IItemBenchmark
    {
        public ItemBenchmarkHelper BenchmarkHelper { get; set; }

        /// <summary>
        /// Benchmark for CreateItemAsync.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Benchmark]
        public async Task CreateItem()
        {
            ItemResponse<ToDoActivity> response = await this.BenchmarkHelper.container.CreateItemAsync<ToDoActivity>(
                this.BenchmarkHelper.testItem,
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
            ItemResponse<ToDoActivity> response = await this.BenchmarkHelper.container.UpsertItemAsync<ToDoActivity>(
                this.BenchmarkHelper.testItem,
                new Cosmos.PartitionKey(Constants.ValidOperationId));

            if ((int)response.StatusCode > 300 || response.Resource == null)
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
                ItemResponse<ToDoActivity> response = await this.BenchmarkHelper.container.ReadItemAsync<ToDoActivity>(
                    Constants.NotFoundOperationId,
                    new Cosmos.PartitionKey(Constants.ValidOperationId));
                throw new Exception();
            }
            catch(CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
            }
        }

        /// <summary>
        /// Benchmark for ReadItemStreamAsync.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Benchmark]
        public async Task ReadItemExists()
        {
            ItemResponse<ToDoActivity> response = await this.BenchmarkHelper.container.ReadItemAsync<ToDoActivity>(
                Constants.ValidOperationId,
                new Cosmos.PartitionKey(Constants.ValidOperationId));

            if ((int)response.StatusCode > 300 || response.Resource == null)
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
            ItemResponse<ToDoActivity> response = await this.BenchmarkHelper.container.ReplaceItemAsync<ToDoActivity>(
                this.BenchmarkHelper.testItem,
                Constants.ValidOperationId,
                new Cosmos.PartitionKey(Constants.ValidOperationId));

            if ((int)response.StatusCode > 300 || response.Resource == null)
            {
                throw new Exception();
            }
        }

        /// <summary>
        /// Benchmark for DeleteItemAsync.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Benchmark]
        public async Task DeleteItemExists()
        {
            ItemResponse<ToDoActivity> response = await this.BenchmarkHelper.container.DeleteItemAsync<ToDoActivity>(
                Constants.ValidOperationId,
                new Cosmos.PartitionKey(Constants.ValidOperationId));

            if ((int)response.StatusCode > 300 || response.Resource == null)
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
                ItemResponse<ToDoActivity> response = await this.BenchmarkHelper.container.DeleteItemAsync<ToDoActivity>(
                    Constants.NotFoundOperationId,
                    new Cosmos.PartitionKey(Constants.ValidOperationId));
            }
            catch(CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
            }
        }

        /// <summary>
        /// Benchmark for DeleteItemAsync.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Benchmark]
        public async Task ReadFeedStream()
        {
            FeedIterator<ToDoActivity> resultIterator = this.BenchmarkHelper.container.GetItemQueryIterator<ToDoActivity>();
            while (resultIterator.HasMoreResults)
            {
                FeedResponse<ToDoActivity> response = await resultIterator.ReadNextAsync();
                if (response.StatusCode != HttpStatusCode.OK || !response.Resource.Any())
                {
                    throw new Exception();
                }
            }
        }
    }

    /// <summary>
    /// Benchmark for Item related operations.
    /// </summary>
    public class ItemBenchmarkHelper
    {
        internal readonly ToDoActivity testItem;
        internal readonly CosmosClient clientForTests;
        internal readonly Container container;
        internal byte[] payloadBytes;

        /// <summary>
        /// Initializes a new instance of the <see cref="ItemBenchmark"/> class.
        /// </summary>
        public ItemBenchmarkHelper(bool useCustomSerialzier = false)
        {
            this.clientForTests = MockDocumentClient.CreateMockCosmosClient(useCustomSerialzier);
            this.container = this.clientForTests.GetDatabase("myDB").GetContainer("myColl");

            using (FileStream tmp = File.OpenRead("samplepayload.json"))
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    tmp.CopyTo(ms);
                    this.payloadBytes = ms.ToArray();
                }
            }

            using (MemoryStream ms = new MemoryStream(this.payloadBytes))
            {
                string payloadContent = File.ReadAllText("samplepayload.json");
                this.testItem = JsonConvert.DeserializeObject<ToDoActivity>(payloadContent);
            }
        }

        public class ToDoActivity
        {
            public string id { get; set; }
            public int taskNum { get; set; }
            public double cost { get; set; }
            public string description { get; set; }
            public string status { get; set; }
            public string CamelCase { get; set; }

            public bool valid { get; set; }

            public ToDoActivity[] children { get; set; }

            public override bool Equals(Object obj)
            {
                ToDoActivity input = obj as ToDoActivity;
                if (input == null)
                {
                    return false;
                }

                return string.Equals(this.id, input.id)
                    && this.taskNum == input.taskNum
                    && this.cost == input.cost
                    && string.Equals(this.description, input.description)
                    && string.Equals(this.status, input.status);
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }

            public static async Task<IList<ToDoActivity>> CreateRandomItems(Container container,
                int pkCount,
                int perPKItemCount = 1,
                bool randomPartitionKey = true)
            {
                List<ToDoActivity> createdList = new List<ToDoActivity>();
                for (int i = 0; i < pkCount; i++)
                {
                    string pk = "TBD";
                    if (randomPartitionKey)
                    {
                        pk += Guid.NewGuid().ToString();
                    }

                    for (int j = 0; j < perPKItemCount; j++)
                    {
                        ToDoActivity temp = ToDoActivity.CreateRandomToDoActivity(pk);

                        createdList.Add(temp);

                        await container.CreateItemAsync<ToDoActivity>(item: temp);
                    }
                }

                return createdList;
            }

            public static ToDoActivity CreateRandomToDoActivity(string pk = null, string id = null)
            {
                if (string.IsNullOrEmpty(pk))
                {
                    pk = "TBD" + Guid.NewGuid().ToString();
                }
                if (id == null)
                {
                    id = Guid.NewGuid().ToString();
                }
                return new ToDoActivity()
                {
                    id = id,
                    description = "CreateRandomToDoActivity",
                    status = pk,
                    taskNum = 42,
                    cost = double.MaxValue,
                    CamelCase = "camelCase",
                    children = new ToDoActivity[]
                    { new ToDoActivity { id = "child1", taskNum = 30 },
                new ToDoActivity { id = "child2", taskNum = 40}
                    },
                    valid = true
                };
            }
        }
    }
}