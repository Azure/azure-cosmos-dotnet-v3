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
    /// Runs a query scoped to a single <see cref="FeedRange"/> obtained from
    /// <see cref="Container.GetFeedRangesAsync(System.Threading.CancellationToken)"/>.
    /// This exercises the partition-scoped query path used by parallel/partitioned
    /// consumers (one feed range per compute unit).
    /// </summary>
    internal class FeedRangeQueryV3BenchmarkOperation : IBenchmarkOperation
    {
        private readonly Container container;
        private readonly string partitionKeyPath;
        private readonly Dictionary<string, object> sampleJObject;

        private readonly string databaseName;
        private readonly string containerName;

        private IReadOnlyList<FeedRange> feedRanges;
        private int feedRangeIndex;
        private bool initialized = false;

        public FeedRangeQueryV3BenchmarkOperation(
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

        public BenchmarkOperationType OperationType => BenchmarkOperationType.Query;

        public async Task<OperationResult> ExecuteOnceAsync()
        {
            // Round-robin across the available feed ranges so consecutive executions
            // spread the load across all physical partitions.
            FeedRange feedRange = this.feedRanges[this.feedRangeIndex];
            this.feedRangeIndex = (this.feedRangeIndex + 1) % this.feedRanges.Count;

            QueryDefinition queryDefinition = new QueryDefinition("select * from c");

            FeedIterator<Dictionary<string, object>> feedIterator = this.container.GetItemQueryIterator<Dictionary<string, object>>(
                feedRange: feedRange,
                queryDefinition: queryDefinition,
                continuationToken: null,
                requestOptions: new QueryRequestOptions());

            double totalCharge = 0;
            CosmosDiagnostics lastDiagnostics = null;
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<Dictionary<string, object>> feedResponse = await feedIterator.ReadNextAsync();

                totalCharge += feedResponse.Headers.RequestCharge;
                lastDiagnostics = feedResponse.Diagnostics;

                if (feedResponse.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception($"FeedRangeQueryV3BenchmarkOperation failed with {feedResponse.StatusCode}");
                }

                foreach (Dictionary<string, object> item in feedResponse)
                {
                    // No-op check that forces any lazy logic to be executed
                    if (item == null)
                    {
                        throw new Exception("Null item was returned");
                    }
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

            this.feedRangeIndex = 0;
            this.initialized = true;
        }
    }
}
