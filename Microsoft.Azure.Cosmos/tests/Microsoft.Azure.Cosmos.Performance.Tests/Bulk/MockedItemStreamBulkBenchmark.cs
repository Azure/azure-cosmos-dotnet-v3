// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;

    public class MockedItemStreamBulkBenchmark : IItemBulkBenchmark
    {
        private readonly MockedItemBenchmarkHelper benchmarkHelper;

        public MockedItemStreamBulkBenchmark()
        {
            this.benchmarkHelper = new MockedItemBenchmarkHelper(useBulk: true);
        }

        public async Task CreateItem()
        {
            using (MemoryStream ms = this.benchmarkHelper.GetItemPayloadAsStream())
            using (ResponseMessage response = await this.benchmarkHelper.TestContainer.CreateItemStreamAsync(
                ms,
                new Cosmos.PartitionKey(MockedItemBenchmarkHelper.ExistingItemId)))
            {
                if ((int)response.StatusCode > 300 || response.Content == null)
                {
                    throw new Exception();
                }
            }
        }

        public async Task UpsertItem()
        {
            using (MemoryStream ms = this.benchmarkHelper.GetItemPayloadAsStream())
            using (ResponseMessage response = await this.benchmarkHelper.TestContainer.UpsertItemStreamAsync(
                ms,
                new Cosmos.PartitionKey(MockedItemBenchmarkHelper.ExistingItemId)))
            {
                if ((int)response.StatusCode > 300 || response.Content == null)
                {
                    throw new Exception();
                }
            }
        }

        public async Task ReadItemNotExists()
        {
            using (ResponseMessage response = await this.benchmarkHelper.TestContainer.ReadItemStreamAsync(
                MockedItemBenchmarkHelper.NonExistingItemId,
                new Cosmos.PartitionKey(MockedItemBenchmarkHelper.ExistingItemId)))
            {
                if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    throw new Exception();
                }
            }
        }

        public async Task ReadItemExists()
        {
            using (ResponseMessage response = await this.benchmarkHelper.TestContainer.ReadItemStreamAsync(
                MockedItemBenchmarkHelper.ExistingItemId,
                new Cosmos.PartitionKey(MockedItemBenchmarkHelper.ExistingItemId)))
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound || response.Content == null)
                {
                    throw new Exception();
                }
            }
        }

        public async Task ReadItemExistsWithDiagnosticToString()
        {
            using (ResponseMessage response = await this.benchmarkHelper.TestContainer.ReadItemStreamAsync(
                MockedItemBenchmarkHelper.ExistingItemId,
                new Cosmos.PartitionKey(MockedItemBenchmarkHelper.ExistingItemId)))
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound || response.Content == null)
                {
                    throw new Exception();
                }

                string diagnostics = response.Diagnostics.ToString();
                if (string.IsNullOrEmpty(diagnostics))
                {
                    throw new Exception();
                }
            }
        }

        public async Task UpdateItem()
        {
            using (MemoryStream ms = this.benchmarkHelper.GetItemPayloadAsStream())
            using (ResponseMessage response = await this.benchmarkHelper.TestContainer.ReplaceItemStreamAsync(
                ms,
                MockedItemBenchmarkHelper.ExistingItemId,
                new Cosmos.PartitionKey(MockedItemBenchmarkHelper.ExistingItemId)))
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound || response.Content == null)
                {
                    throw new Exception();
                }
            }
        }

        public async Task DeleteItemExists()
        {
            using (ResponseMessage response = await this.benchmarkHelper.TestContainer.DeleteItemStreamAsync(
                MockedItemBenchmarkHelper.ExistingItemId,
                new Cosmos.PartitionKey(MockedItemBenchmarkHelper.ExistingItemId)))
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    throw new Exception();
                }
            }
        }

        public async Task DeleteItemNotExists()
        {
            using (ResponseMessage response = await this.benchmarkHelper.TestContainer.DeleteItemStreamAsync(
                MockedItemBenchmarkHelper.NonExistingItemId,
                new Cosmos.PartitionKey(MockedItemBenchmarkHelper.ExistingItemId)))
            {
                if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    throw new Exception();
                }
            }
        }
    }
}
