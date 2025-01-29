// ----------------------------------------------------------------
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
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    public class MockedItemStreamBenchmark : IItemBenchmark
    {
        public MockedItemBenchmarkHelper BenchmarkHelper { get; set; }

        public async Task CreateItem()
        {
            using (MemoryStream ms = this.BenchmarkHelper.GetItemPayloadAsStream())
            using (ResponseMessage response = await this.BenchmarkHelper.TestContainer.CreateItemStreamAsync(
                ms,
                new Cosmos.PartitionKey(MockedItemBenchmarkHelper.ExistingItemId)))
            {
                if ((int)response.StatusCode > 300 || response.Content == null)
                {
                    throw new Exception($"Failed with status code {response.StatusCode}");
                }

                this.VerifyBinaryHeaderIfExpected(response);
            }
        }
        public async Task UpsertItem()
        {
            using (MemoryStream ms = this.BenchmarkHelper.GetItemPayloadAsStream())
            using (ResponseMessage response = await this.BenchmarkHelper.TestContainer.UpsertItemStreamAsync(
                ms,
                new Cosmos.PartitionKey(MockedItemBenchmarkHelper.ExistingItemId)))
            {
                if ((int)response.StatusCode > 300 || response.Content == null)
                {
                    throw new Exception($"Failed with status code {response.StatusCode}");
                }

                this.VerifyBinaryHeaderIfExpected(response);
            }
        }

        public async Task ReadItemNotExists()
        {
            using (ResponseMessage response = await this.BenchmarkHelper.TestContainer.ReadItemStreamAsync(
                MockedItemBenchmarkHelper.NonExistingItemId,
                new Cosmos.PartitionKey(MockedItemBenchmarkHelper.ExistingItemId)))
            {
                if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    this.VerifyBinaryHeaderIfExpected(response);

                    throw new Exception($"Failed with status code {response.StatusCode}");
                }
            }
        }

        public async Task ReadItemExists()
        {
            using (ResponseMessage response = await this.BenchmarkHelper.TestContainer.ReadItemStreamAsync(
                MockedItemBenchmarkHelper.ExistingItemId,
                new Cosmos.PartitionKey(MockedItemBenchmarkHelper.ExistingItemId)))
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound || response.Content == null)
                {
                    throw new Exception($"Failed with status code {response.StatusCode}");
                }

                this.VerifyBinaryHeaderIfExpected(response);
            }
        }

        public async Task ReadItemExistsWithDiagnosticToString()
        {
            using (ResponseMessage response = await this.BenchmarkHelper.TestContainer.ReadItemStreamAsync(
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
            using (MemoryStream ms = this.BenchmarkHelper.GetItemPayloadAsStream())
            using (ResponseMessage response = await this.BenchmarkHelper.TestContainer.ReplaceItemStreamAsync(
                ms,
                MockedItemBenchmarkHelper.ExistingItemId,
                new Cosmos.PartitionKey(MockedItemBenchmarkHelper.ExistingItemId)))
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound || response.Content == null)
                {
                    throw new Exception($"Failed with status code {response.StatusCode}");
                }

                this.VerifyBinaryHeaderIfExpected(response);
            }
        }

        public async Task DeleteItemExists()
        {
            using (ResponseMessage response = await this.BenchmarkHelper.TestContainer.DeleteItemStreamAsync(
                MockedItemBenchmarkHelper.ExistingItemId,
                new Cosmos.PartitionKey(MockedItemBenchmarkHelper.ExistingItemId)))
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    throw new Exception($"Failed with status code {response.StatusCode}");
                }

                this.VerifyBinaryHeaderIfExpected(response);
            }
        }

        public async Task DeleteItemNotExists()
        {
            using (ResponseMessage response = await this.BenchmarkHelper.TestContainer.DeleteItemStreamAsync(
                MockedItemBenchmarkHelper.NonExistingItemId,
                new Cosmos.PartitionKey(MockedItemBenchmarkHelper.ExistingItemId)))
            {
                if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    this.VerifyBinaryHeaderIfExpected(response);

                    throw new Exception($"Failed with status code {response.StatusCode}");
                }
            }
        }

        public async Task ReadFeed()
        {
            using FeedIterator streamIterator = this.BenchmarkHelper.TestContainer.GetItemQueryStreamIterator();
            while (streamIterator.HasMoreResults)
            {
                using ResponseMessage response = await streamIterator.ReadNextAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception($"Failed with status code {response.StatusCode}");
                }
            }
        }

        public async Task QuerySinglePartitionOnePage()
        {
            using FeedIterator streamIterator = this.BenchmarkHelper.TestContainer.GetItemQueryStreamIterator(
                "select * from T",
                requestOptions: new QueryRequestOptions()
                {
                    PartitionKey = new Cosmos.PartitionKey("dummyValue"),
                });
            while (streamIterator.HasMoreResults)
            {
                using ResponseMessage response = await streamIterator.ReadNextAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception($"Failed with status code {response.StatusCode}");
                }
            }
        }

        public async Task QuerySinglePartitionMultiplePages()
        {
            using FeedIterator streamIterator = this.BenchmarkHelper.TestContainer.GetItemQueryStreamIterator(
              "select * from T",
              requestOptions: new QueryRequestOptions()
              {
                  MaxItemCount = 1,
                  PartitionKey = new Cosmos.PartitionKey("dummyValue"),
              });

            while (streamIterator.HasMoreResults)
            {
                using ResponseMessage response = await streamIterator.ReadNextAsync();

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception($"Failed with status code {response.StatusCode}");
                }
            }
        }

        private void VerifyBinaryHeaderIfExpected(ResponseMessage response)
        {
            if (this.BenchmarkHelper.EnableBinaryEncoding)
            {
                string headerValue = response.Headers.GetValueOrDefault(HttpConstants.HttpHeaders.SupportedSerializationFormats);
                if (headerValue != "CosmosBinary")
                {
                    // If we expected binary but got something else, fail.
                    throw new InvalidOperationException(
                        $"Expected response with 'CosmosBinary' format, but got '{headerValue ?? "<null>"}'.");
                }
            }
        }
    }
}
