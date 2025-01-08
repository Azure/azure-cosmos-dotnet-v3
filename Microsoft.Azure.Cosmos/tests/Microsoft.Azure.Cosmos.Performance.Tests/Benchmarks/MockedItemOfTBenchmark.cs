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
    using Microsoft.Azure.Documents;

    public class MockedItemOfTBenchmark : IItemBenchmark
    {
        public MockedItemBenchmarkHelper BenchmarkHelper { get; set; }

        public async Task CreateItem()
        {
            ItemResponse<ToDoActivity> response = await this.BenchmarkHelper.TestContainer.CreateItemAsync(
                this.BenchmarkHelper.TestItem,
                MockedItemBenchmarkHelper.ExistingPartitionId);

            if ((int)response.StatusCode > 300 || response.Resource == null)
            {
                throw new Exception($"Failed with status code {response.StatusCode}");
            }

            this.VerifyBinaryHeaderIfExpected(response);

            this.BenchmarkHelper.IncludeDiagnosticToStringHelper(response.Diagnostics);
        }

        public async Task UpsertItem()
        {
            ItemResponse<ToDoActivity> response = await this.BenchmarkHelper.TestContainer.UpsertItemAsync(
                this.BenchmarkHelper.TestItem,
                MockedItemBenchmarkHelper.ExistingPartitionId);

            if ((int)response.StatusCode > 300 || response.Resource == null)
            {
                throw new Exception($"Failed with status code {response.StatusCode}");
            }

            this.VerifyBinaryHeaderIfExpected(response);

            this.BenchmarkHelper.IncludeDiagnosticToStringHelper(response.Diagnostics);
        }

        public async Task ReadItemNotExists()
        {
            try
            {
                ItemResponse<ToDoActivity> response = await this.BenchmarkHelper.TestContainer.ReadItemAsync<ToDoActivity>(
                    MockedItemBenchmarkHelper.NonExistingItemId,
                    MockedItemBenchmarkHelper.ExistingPartitionId);
                throw new Exception($"Failed with status code {response.StatusCode}");
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
                throw new Exception($"Failed with status code {response.StatusCode}");
            }

            this.VerifyBinaryHeaderIfExpected(response);

            this.BenchmarkHelper.IncludeDiagnosticToStringHelper(response.Diagnostics);
        }

        public async Task UpdateItem()
        {
            ItemResponse<ToDoActivity> response = await this.BenchmarkHelper.TestContainer.ReplaceItemAsync(
                this.BenchmarkHelper.TestItem,
                MockedItemBenchmarkHelper.ExistingItemId,
                MockedItemBenchmarkHelper.ExistingPartitionId);

            if ((int)response.StatusCode > 300 || response.Resource == null)
            {
                throw new Exception($"Failed with status code {response.StatusCode}");
            }

            this.VerifyBinaryHeaderIfExpected(response);

            this.BenchmarkHelper.IncludeDiagnosticToStringHelper(response.Diagnostics);
        }

        public async Task DeleteItemExists()
        {
            ItemResponse<ToDoActivity> response = await this.BenchmarkHelper.TestContainer.DeleteItemAsync<ToDoActivity>(
                MockedItemBenchmarkHelper.ExistingItemId,
                MockedItemBenchmarkHelper.ExistingPartitionId);

            if ((int)response.StatusCode > 300 || response.Resource == null)
            {
                throw new Exception($"Failed with status code {response.StatusCode}");
            }

            this.VerifyBinaryHeaderIfExpected(response);

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
            using FeedIterator<ToDoActivity> resultIterator = this.BenchmarkHelper.TestContainer.GetItemQueryIterator<ToDoActivity>();
            while (resultIterator.HasMoreResults)
            {
                FeedResponse<ToDoActivity> response = await resultIterator.ReadNextAsync();
                if (response.StatusCode != HttpStatusCode.OK || response.Resource.Count() == 0)
                {
                    throw new Exception($"Failed with status code {response.StatusCode}");
                }

                this.BenchmarkHelper.IncludeDiagnosticToStringHelper(response.Diagnostics);
            }
        }

        public async Task QuerySinglePartitionOnePage()
        {
            using FeedIterator<ToDoActivity> resultIterator = this.BenchmarkHelper.TestContainer.GetItemQueryIterator<ToDoActivity>(
                "select * from T",
                requestOptions: new QueryRequestOptions
                {
                    PartitionKey = new Cosmos.PartitionKey("dummyValue"),
                });

            while (resultIterator.HasMoreResults)
            {
                FeedResponse<ToDoActivity> response = await resultIterator.ReadNextAsync();
                if (response.StatusCode != HttpStatusCode.OK || response.Resource.Count() == 0)
                {
                    throw new Exception($"Failed with status code {response.StatusCode}");
                }

                this.BenchmarkHelper.IncludeDiagnosticToStringHelper(response.Diagnostics);
            }
        }

        public async Task QuerySinglePartitionMultiplePages()
        {
            using FeedIterator<ToDoActivity> resultIterator = this.BenchmarkHelper.TestContainer.GetItemQueryIterator<ToDoActivity>(
                "select * from T",
                requestOptions: new QueryRequestOptions
                {
                    MaxItemCount = 1,
                    PartitionKey = new Cosmos.PartitionKey("dummyValue"),
                });

            while (resultIterator.HasMoreResults)
            {
                FeedResponse<ToDoActivity> response = await resultIterator.ReadNextAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception($"Failed with status code {response.StatusCode}");
                }

                this.BenchmarkHelper.IncludeDiagnosticToStringHelper(response.Diagnostics);
            }
        }

        /// <summary>
        /// Verifies that the response used binary encoding if the test was expecting it.
        /// </summary>
        /// <param name="response">ItemResponse from the operation.</param>
        private void VerifyBinaryHeaderIfExpected(ItemResponse<ToDoActivity> response)
        {
            bool isBinaryFlagEnabled = bool.TryParse(
                Environment.GetEnvironmentVariable("COSMOS_ENABLE_BINARY_ENCODING"),
                out bool envValue)
                && envValue;

            if (isBinaryFlagEnabled)
            {
                string formatHeader = response.Headers.GetValueOrDefault(HttpConstants.HttpHeaders.SupportedSerializationFormats);
                if (formatHeader == SupportedSerializationFormats.CosmosBinary.ToString())
                {
                    // Confirmed binary encoding for this point operation
                    // (You might place a breakpoint here or track a local debug variable.)
                }
                else
                {
                    // If you want to fail if the header doesn't match, throw or assert here.
                    // throw new Exception("Expected binary but did not see header...");
                }
            }
        }
    }
}
