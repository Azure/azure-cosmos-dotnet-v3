﻿// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos;

    public class MockedItemStreamBenchmark : IItemBenchmark
    {
        private readonly MockedItemBenchmarkHelper benchmarkHelper;

        public MockedItemStreamBenchmark()
        {
            this.benchmarkHelper = new MockedItemBenchmarkHelper();
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
                    throw new Exception($"Failed with status code {response.StatusCode}");
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
                    throw new Exception($"Failed with status code {response.StatusCode}");
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
                    throw new Exception($"Failed with status code {response.StatusCode}");
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
                    throw new Exception($"Failed with status code {response.StatusCode}");
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
                    throw new Exception($"Failed with status code {response.StatusCode}");
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
                    throw new Exception($"Failed with status code {response.StatusCode}");
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
                    throw new Exception($"Failed with status code {response.StatusCode}");
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
                    throw new Exception($"Failed with status code {response.StatusCode}");
                }
            }
        }

        public async Task ReadFeed()
        {
            using FeedIterator streamIterator = this.benchmarkHelper.TestContainer.GetItemQueryStreamIterator();
            while (streamIterator.HasMoreResults)
            {
                ResponseMessage response = await streamIterator.ReadNextAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception($"Failed with status code {response.StatusCode}");
                }
            }
        }

        public async Task QuerySinglePartitionOnePage()
        {
            using FeedIterator streamIterator = this.benchmarkHelper.TestContainer.GetItemQueryStreamIterator(
                "select * from T",
                requestOptions: new QueryRequestOptions()
                {
                    PartitionKey = new PartitionKey("dummyValue"),
                });
            while (streamIterator.HasMoreResults)
            {
                ResponseMessage response = await streamIterator.ReadNextAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception($"Failed with status code {response.StatusCode}");
                }
            }
        }

        public async Task QuerySinglePartitionMultiplePages()
        {
            using FeedIterator streamIterator = this.benchmarkHelper.TestContainer.GetItemQueryStreamIterator(
              "select * from T",
              requestOptions: new QueryRequestOptions()
              {
                  MaxItemCount = 1,
                  PartitionKey = new PartitionKey("dummyValue"),
              });

            while (streamIterator.HasMoreResults)
            {
                ResponseMessage response = await streamIterator.ReadNextAsync();

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception($"Failed with status code {response.StatusCode}");
                }
            }
        }

    }
}
