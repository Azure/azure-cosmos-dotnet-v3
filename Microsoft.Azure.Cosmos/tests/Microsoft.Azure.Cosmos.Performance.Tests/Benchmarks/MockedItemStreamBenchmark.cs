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
                    this.AssertResponseType(response.Content, JsonSerializationFormat.Binary);
                }
                else
                {
                    this.AssertResponseType(response.Content, JsonSerializationFormat.Text);
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

                // This test specifically expects text, even if EnableBinaryEncoding is true.
                if (this.BenchmarkHelper.EnableBinaryEncoding)
                {
                    this.AssertResponseType(response.Content, JsonSerializationFormat.Text);
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
                    this.AssertResponseType(response.Content, JsonSerializationFormat.Binary);
                }
                else
                {
                    this.AssertResponseType(response.Content, JsonSerializationFormat.Text);
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
                if (response.StatusCode != HttpStatusCode.NotFound && this.BenchmarkHelper.EnableBinaryEncoding)
                {
                    this.AssertResponseType(response.Content, JsonSerializationFormat.Binary);
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
                if (response.StatusCode == HttpStatusCode.NotFound || response.Content == null)
                {
                    throw new Exception($"Failed with status code {response.StatusCode}");
                }

                if (this.BenchmarkHelper.EnableBinaryEncoding)
                {
                    this.AssertResponseType(response.Content, JsonSerializationFormat.Binary);
                }
                else
                {
                    this.AssertResponseType(response.Content, JsonSerializationFormat.Text);
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
                if (response.StatusCode == HttpStatusCode.NotFound || response.Content == null)
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
                    this.AssertResponseType(response.Content, JsonSerializationFormat.Binary);
                }
                else
                {
                    this.AssertResponseType(response.Content, JsonSerializationFormat.Text);
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
                if (response.StatusCode == HttpStatusCode.NotFound || response.Content == null)
                {
                    throw new Exception($"Failed with status code {response.StatusCode}");
                }

                if (this.BenchmarkHelper.EnableBinaryEncoding)
                {
                    this.AssertResponseType(response.Content, JsonSerializationFormat.Binary);
                }
                else
                {
                    this.AssertResponseType(response.Content, JsonSerializationFormat.Text);
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
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new Exception($"Failed with status code {response.StatusCode}");
                }

                if (this.BenchmarkHelper.EnableBinaryEncoding)
                {
                    this.AssertResponseType(response.Content, JsonSerializationFormat.Binary);
                }
                else
                {
                    this.AssertResponseType(response.Content, JsonSerializationFormat.Text);
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
                if (response.StatusCode != HttpStatusCode.NotFound && this.BenchmarkHelper.EnableBinaryEncoding)
                {
                    this.AssertResponseType(response.Content, JsonSerializationFormat.Binary);
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

        private void AssertResponseType(Stream responseStream, JsonSerializationFormat expectedFormat)
        {
            if (responseStream == null)
            {
                throw new ArgumentNullException(nameof(responseStream));
            }

            // Ensure the stream is seekable
            if (!responseStream.CanSeek)
            {
                // Wrap the responseStream in a MemoryStream
                MemoryStream memoryStream = new MemoryStream();
                responseStream.CopyTo(memoryStream);
                memoryStream.Position = 0;
                responseStream = memoryStream;
            }

            long originalPosition = responseStream.Position;

            int firstByte = responseStream.ReadByte();

            // Reset the position after reading
            responseStream.Position = originalPosition;

            if (firstByte == -1)
            {
                throw new InvalidOperationException("Response stream is empty.");
            }

            bool isExpectedFormat = this.IsExpectedSerializationFormat(firstByte, expectedFormat);

            if (!isExpectedFormat)
            {
                throw new InvalidOperationException(
                    $"Response content format does not match expected format: {expectedFormat}");
            }
        }

        private bool IsExpectedSerializationFormat(int firstByte, JsonSerializationFormat expectedFormat)
        {
            if (expectedFormat == JsonSerializationFormat.Binary)
            {
                return firstByte == (int)JsonSerializationFormat.Binary;
            }
            else if (expectedFormat == JsonSerializationFormat.Text)
            {
                return firstByte != (int)JsonSerializationFormat.Binary;
            }

            return false;
        }
    }
}
