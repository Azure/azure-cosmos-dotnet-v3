// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    public class MockedItemStreamBenchmark : IItemBenchmark
    {
        public MockedItemBenchmarkHelper BenchmarkHelper { get; set; }

        public async Task CreateItem()
        {
            ItemRequestOptions requestOptions = null;
            if (this.BenchmarkHelper.EnableBinaryEncoding)
            {
                requestOptions = new ItemRequestOptions
                {
                    EnableBinaryResponseOnPointOperations = true
                };
            }
            using (MemoryStream ms = this.BenchmarkHelper.GetItemPayloadAsStream())
            using (ResponseMessage response = await this.BenchmarkHelper.TestContainer.CreateItemStreamAsync(
                ms,
                new Cosmos.PartitionKey(MockedItemBenchmarkHelper.ExistingItemId),
                requestOptions))
            {
                if ((int)response.StatusCode > 300 || response.Content == null)
                {
                    throw new Exception($"Failed with status code {response.StatusCode}");
                }

                if (this.BenchmarkHelper.EnableBinaryEncoding)
                {
                    this.AssertBinaryResponseType(response.Content);
                }
            }
        }

        public async Task CreateStreamItem_SetBinaryResponseOnPointOperationsFalse_ReturnsText()
        {
            ItemRequestOptions requestOptions = null;
            if (this.BenchmarkHelper.EnableBinaryEncoding)
            {
                requestOptions = new ItemRequestOptions
                {
                    EnableBinaryResponseOnPointOperations = false
                };
            }

            using (MemoryStream ms = this.BenchmarkHelper.GetItemPayloadAsStream())
            using (ResponseMessage response = await this.BenchmarkHelper.TestContainer.CreateItemStreamAsync(
                ms,
                new Cosmos.PartitionKey(MockedItemBenchmarkHelper.ExistingItemId),
                requestOptions))
            {
                if ((int)response.StatusCode > 300 || response.Content == null)
                {
                    throw new Exception($"Failed with status code {response.StatusCode}");
                }
                
                if (this.BenchmarkHelper.EnableBinaryEncoding)
                {
                    this.AssertTextResponseType(response.Content);
                }
            }
        }

        public async Task UpsertItem()
        {
            ItemRequestOptions requestOptions = null;
            if (this.BenchmarkHelper.EnableBinaryEncoding)
            {
                requestOptions = new ItemRequestOptions
                {
                    EnableBinaryResponseOnPointOperations = true
                };
            }
            using (MemoryStream ms = this.BenchmarkHelper.GetItemPayloadAsStream())
            using (ResponseMessage response = await this.BenchmarkHelper.TestContainer.UpsertItemStreamAsync(
                ms,
                new Cosmos.PartitionKey(MockedItemBenchmarkHelper.ExistingItemId),
                requestOptions))
            {
                if ((int)response.StatusCode > 300 || response.Content == null)
                {
                    throw new Exception($"Failed with status code {response.StatusCode}");
                }

                if (this.BenchmarkHelper.EnableBinaryEncoding)
                {
                    this.AssertBinaryResponseType(response.Content);
                }
            }
        }

        public async Task ReadItemNotExists()
        {
            ItemRequestOptions requestOptions = null;
            if (this.BenchmarkHelper.EnableBinaryEncoding)
            {
                requestOptions = new ItemRequestOptions
                {
                    EnableBinaryResponseOnPointOperations = true
                };
            }
            using (ResponseMessage response = await this.BenchmarkHelper.TestContainer.ReadItemStreamAsync(
                MockedItemBenchmarkHelper.NonExistingItemId,
                new Cosmos.PartitionKey(MockedItemBenchmarkHelper.ExistingItemId),
                requestOptions))
            {
                if (response.StatusCode != System.Net.HttpStatusCode.NotFound && this.BenchmarkHelper.EnableBinaryEncoding)
                {
                    this.AssertBinaryResponseType(response.Content);

                    throw new Exception($"Failed with status code {response.StatusCode}");
                }
            }
        }

        public async Task ReadItemExists()
        {
            ItemRequestOptions requestOptions = null;
            if (this.BenchmarkHelper.EnableBinaryEncoding)
            {
                requestOptions = new ItemRequestOptions
                {
                    EnableBinaryResponseOnPointOperations = true
                };
            }
            using (ResponseMessage response = await this.BenchmarkHelper.TestContainer.ReadItemStreamAsync(
                MockedItemBenchmarkHelper.ExistingItemId,
                new Cosmos.PartitionKey(MockedItemBenchmarkHelper.ExistingItemId),
                requestOptions))
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound || response.Content == null)
                {
                    throw new Exception($"Failed with status code {response.StatusCode}");
                }

                if (this.BenchmarkHelper.EnableBinaryEncoding)
                {
                    this.AssertBinaryResponseType(response.Content);
                }
            }
        }

        public async Task ReadItemExistsWithDiagnosticToString()
        {
            ItemRequestOptions requestOptions = null;
            if (this.BenchmarkHelper.EnableBinaryEncoding)
            {
                requestOptions = new ItemRequestOptions
                {
                    EnableBinaryResponseOnPointOperations = true
                };
            }
            using (ResponseMessage response = await this.BenchmarkHelper.TestContainer.ReadItemStreamAsync(
                MockedItemBenchmarkHelper.ExistingItemId,
                new Cosmos.PartitionKey(MockedItemBenchmarkHelper.ExistingItemId),
                requestOptions))
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

                if (this.BenchmarkHelper.EnableBinaryEncoding)
                {
                    this.AssertBinaryResponseType(response.Content);
                }
            }
        }

        public async Task UpdateItem()
        {
            ItemRequestOptions requestOptions = null;
            if (this.BenchmarkHelper.EnableBinaryEncoding)
            {
                requestOptions = new ItemRequestOptions
                {
                    EnableBinaryResponseOnPointOperations = true
                };
            }
            using (MemoryStream ms = this.BenchmarkHelper.GetItemPayloadAsStream())
            using (ResponseMessage response = await this.BenchmarkHelper.TestContainer.ReplaceItemStreamAsync(
                ms,
                MockedItemBenchmarkHelper.ExistingItemId,
                new Cosmos.PartitionKey(MockedItemBenchmarkHelper.ExistingItemId),
                requestOptions))
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound || response.Content == null)
                {
                    throw new Exception($"Failed with status code {response.StatusCode}");
                }

                if (this.BenchmarkHelper.EnableBinaryEncoding)
                {
                    this.AssertBinaryResponseType(response.Content);
                }
            }
        }

        public async Task DeleteItemExists()
        {
            ItemRequestOptions requestOptions = null;
            if (this.BenchmarkHelper.EnableBinaryEncoding)
            {
                requestOptions = new ItemRequestOptions
                {
                    EnableBinaryResponseOnPointOperations = true
                };
            }
            using (ResponseMessage response = await this.BenchmarkHelper.TestContainer.DeleteItemStreamAsync(
                MockedItemBenchmarkHelper.ExistingItemId,
                new Cosmos.PartitionKey(MockedItemBenchmarkHelper.ExistingItemId),
                requestOptions))
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    throw new Exception($"Failed with status code {response.StatusCode}");
                }

                if (this.BenchmarkHelper.EnableBinaryEncoding)
                {
                    this.AssertBinaryResponseType(response.Content);
                }
            }
        }

        public async Task DeleteItemNotExists()
        {
            ItemRequestOptions requestOptions = null;
            if (this.BenchmarkHelper.EnableBinaryEncoding)
            {
                requestOptions = new ItemRequestOptions
                {
                    EnableBinaryResponseOnPointOperations = true
                };
            }
            using (ResponseMessage response = await this.BenchmarkHelper.TestContainer.DeleteItemStreamAsync(
                MockedItemBenchmarkHelper.NonExistingItemId,
                new Cosmos.PartitionKey(MockedItemBenchmarkHelper.ExistingItemId),
                requestOptions))
            {
                if (response.StatusCode != System.Net.HttpStatusCode.NotFound && this.BenchmarkHelper.EnableBinaryEncoding)
                {
                    this.AssertBinaryResponseType(response.Content);

                    throw new Exception($"Failed with status code {response.StatusCode}");
                }
            }
        }


        public async Task ReadFeed()
        {
            using FeedIterator streamIterator = this.BenchmarkHelper.TestContainer.GetItemQueryStreamIterator();
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
            using FeedIterator streamIterator = this.BenchmarkHelper.TestContainer.GetItemQueryStreamIterator(
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
            using FeedIterator streamIterator = this.BenchmarkHelper.TestContainer.GetItemQueryStreamIterator(
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

        private void AssertBinaryResponseType(Stream responseStream)
        {
            if (responseStream != null)
            {
                MemoryStream binaryStream = new();
                responseStream.CopyTo(binaryStream);
                byte[] content = binaryStream.ToArray();
                responseStream.Position = 0;
                Assert.IsTrue(content.Length > 0);
                Assert.IsTrue(IsBinaryFormat(content[0], JsonSerializationFormat.Binary), "Not Binary format.");
            }
        }
        private void AssertTextResponseType(Stream responseStream)
        {
            if (responseStream != null)
            {
                MemoryStream binaryStream = new();
                responseStream.CopyTo(binaryStream);
                byte[] content = binaryStream.ToArray();
                responseStream.Position = 0;
                Assert.IsTrue(content.Length > 0);
                Assert.IsTrue(IsTextFormat(content[0], JsonSerializationFormat.Text), "Not Text format.");
            }
        }

        private static bool IsBinaryFormat(
            int firstByte,
            JsonSerializationFormat desiredFormat)
        {
            return desiredFormat == JsonSerializationFormat.Binary && firstByte == (int)JsonSerializationFormat.Binary;
        }

        private static bool IsTextFormat(
            int firstByte,
            JsonSerializationFormat desiredFormat)
        {
            return desiredFormat == JsonSerializationFormat.Text && firstByte < (int)JsonSerializationFormat.Binary;
        }
    }
}
