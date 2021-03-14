// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;

    public class MockedItemOfTBenchmark : IItemBenchmark
    {
        public MockedItemBenchmarkHelper BenchmarkHelper { get; set; }

        public async Task CreateItem()
        {
            ItemResponse<ToDoActivity> response = await this.BenchmarkHelper.TestContainer.CreateItemAsync<ToDoActivity>(
                this.BenchmarkHelper.TestItem,
                MockedItemBenchmarkHelper.ExistingPartitionId);

            if ((int)response.StatusCode > 300 || response.Resource == null)
            {
                throw new Exception();
            }

            this.BenchmarkHelper.IncludeDiagnosticToStringHelper(response.Diagnostics);
        }

        public async Task UpsertItem()
        {
            ItemResponse<ToDoActivity> response = await this.BenchmarkHelper.TestContainer.UpsertItemAsync<ToDoActivity>(
                this.BenchmarkHelper.TestItem,
                MockedItemBenchmarkHelper.ExistingPartitionId);

            if ((int)response.StatusCode > 300 || response.Resource == null)
            {
                throw new Exception();
            }

            this.BenchmarkHelper.IncludeDiagnosticToStringHelper(response.Diagnostics);
        }

        public async Task ReadItemNotExists()
        {
            try
            {
                ItemResponse<ToDoActivity> response = await this.BenchmarkHelper.TestContainer.ReadItemAsync<ToDoActivity>(
                    MockedItemBenchmarkHelper.NonExistingItemId,
                    MockedItemBenchmarkHelper.ExistingPartitionId);
                throw new Exception();
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                this.BenchmarkHelper.IncludeDiagnosticToStringHelper(ex.Diagnostics);
            }
        }

        public async Task ReadItemExists()
        {
            ItemResponse<ToDoActivity> response = await this.BenchmarkHelper.TestContainer.ReadItemAsync<ToDoActivity>(
                MockedItemBenchmarkHelper.ExistingItemId,
                MockedItemBenchmarkHelper.ExistingPartitionId);

            if ((int)response.StatusCode > 300 || response.Resource == null)
            {
                throw new Exception();
            }

            this.BenchmarkHelper.IncludeDiagnosticToStringHelper(response.Diagnostics);
        }

        public async Task UpdateItem()
        {
            ItemResponse<ToDoActivity> response = await this.BenchmarkHelper.TestContainer.ReplaceItemAsync<ToDoActivity>(
                this.BenchmarkHelper.TestItem,
                MockedItemBenchmarkHelper.ExistingItemId,
                MockedItemBenchmarkHelper.ExistingPartitionId);

            if ((int)response.StatusCode > 300 || response.Resource == null)
            {
                throw new Exception();
            }

            this.BenchmarkHelper.IncludeDiagnosticToStringHelper(response.Diagnostics);
        }

        public async Task DeleteItemExists()
        {
            ItemResponse<ToDoActivity> response = await this.BenchmarkHelper.TestContainer.DeleteItemAsync<ToDoActivity>(
                MockedItemBenchmarkHelper.ExistingItemId,
                MockedItemBenchmarkHelper.ExistingPartitionId);

            if ((int)response.StatusCode > 300 || response.Resource == null)
            {
                throw new Exception();
            }

            this.BenchmarkHelper.IncludeDiagnosticToStringHelper(response.Diagnostics);
        }

        public async Task DeleteItemNotExists()
        {
            try
            {
                ItemResponse<ToDoActivity> response = await this.BenchmarkHelper.TestContainer.DeleteItemAsync<ToDoActivity>(
                    MockedItemBenchmarkHelper.NonExistingItemId,
                    MockedItemBenchmarkHelper.ExistingPartitionId);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                this.BenchmarkHelper.IncludeDiagnosticToStringHelper(ex.Diagnostics);
            }
        }

        public async Task ReadFeed()
        {
            FeedIterator<ToDoActivity> resultIterator = this.BenchmarkHelper.TestContainer.GetItemQueryIterator<ToDoActivity>();
            while (resultIterator.HasMoreResults)
            {
                FeedResponse<ToDoActivity> response = await resultIterator.ReadNextAsync();
                if (response.StatusCode != HttpStatusCode.OK || response.Resource.Count() == 0)
                {
                    throw new Exception();
                }

                this.BenchmarkHelper.IncludeDiagnosticToStringHelper(response.Diagnostics);
            }
        }
    }
}
