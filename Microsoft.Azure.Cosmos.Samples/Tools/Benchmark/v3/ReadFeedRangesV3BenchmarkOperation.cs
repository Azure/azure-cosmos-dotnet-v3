//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// Enumerates every <see cref="FeedRange"/> of the container (via
    /// <see cref="Container.GetFeedRangesAsync(System.Threading.CancellationToken)"/>) and reads
    /// the contents of each range with a ReadFeed (change feed from the beginning).
    /// This exercises the feed-range enumeration / partition-parallel full-scan path.
    /// </summary>
    internal class ReadFeedRangesV3BenchmarkOperation : IBenchmarkOperation
    {
        private readonly Container container;
        private readonly string partitionKeyPath;
        private readonly Dictionary<string, object> sampleJObject;

        private readonly string databaseName;
        private readonly string containerName;

        private IReadOnlyList<FeedRange> feedRanges;
        private bool initialized = false;

        public ReadFeedRangesV3BenchmarkOperation(
            CosmosClient cosmosClient,
            string dbName,
            string containerName,
            string partitionKeyPath,
            string sampleJson)
        {
            this.databaseName = dbName;
            this.containerName = containerName;

            this.container = cosmosClient.GetContainer(this.databaseName, this.containerName);
            this.partitionKeyPath = partitionKeyPath.Replace("/", "");

            this.sampleJObject = JsonHelper.Deserialize<Dictionary<string, object>>(sampleJson);
        }

        public BenchmarkOperationType OperationType => BenchmarkOperationType.Read;

        public async Task<OperationResult> ExecuteOnceAsync()
        {
            double totalCharge = 0;
            CosmosDiagnostics lastDiagnostics = null;

            foreach (FeedRange feedRange in this.feedRanges)
            {
                using FeedIterator feedIterator = this.container.GetChangeFeedStreamIterator(
                    changeFeedStartFrom: ChangeFeedStartFrom.Beginning(feedRange),
                    changeFeedMode: ChangeFeedMode.Incremental);

                // Drain the current contents of the range. The change feed returns
                // NotModified (304) once the caller has caught up with the range.
                while (feedIterator.HasMoreResults)
                {
                    using ResponseMessage feedResponse = await feedIterator.ReadNextAsync();

                    totalCharge += feedResponse.Headers.RequestCharge;
                    lastDiagnostics = feedResponse.Diagnostics;

                    if (feedResponse.StatusCode == HttpStatusCode.NotModified)
                    {
                        // Caught up with this range; move on to the next one.
                        break;
                    }

                    if (feedResponse.StatusCode != HttpStatusCode.OK)
                    {
                        throw new Exception($"ReadFeedRangesV3BenchmarkOperation failed with {feedResponse.StatusCode}");
                    }

                    // Access the stream to force any lazy logic to be executed.
                    using Stream stream = feedResponse.Content;
                }
            }

            return new OperationResult()
            {
                DatabseName = this.databaseName,
                ContainerName = this.containerName,
                OperationType = this.OperationType,
                RuCharges = totalCharge,
                CosmosDiagnostics = lastDiagnostics,
                LazyDiagnostics = () => lastDiagnostics?.ToString(),
            };
        }

        public async Task PrepareAsync()
        {
            if (this.initialized)
            {
                return;
            }

            for (int itemCount = 0; itemCount < 100; itemCount++)
            {
                string objectId = Guid.NewGuid().ToString();
                string partitionKey = Guid.NewGuid().ToString();

                this.sampleJObject["id"] = objectId;
                this.sampleJObject[this.partitionKeyPath] = partitionKey;

                using (MemoryStream inputStream = JsonHelper.ToStream(this.sampleJObject))
                {
                    using ResponseMessage itemResponse = await this.container.CreateItemStreamAsync(
                            inputStream,
                            new PartitionKey(partitionKey));

                    System.Buffers.ArrayPool<byte>.Shared.Return(inputStream.GetBuffer());

                    if (itemResponse.StatusCode != HttpStatusCode.Created)
                    {
                        throw new Exception($"Create failed with status code: {itemResponse.StatusCode}");
                    }
                }
            }

            this.feedRanges = await this.container.GetFeedRangesAsync();
            if (this.feedRanges == null || this.feedRanges.Count == 0)
            {
                throw new Exception("GetFeedRangesAsync returned no feed ranges");
            }

            this.initialized = true;
        }
    }
}
