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

    internal abstract class QueryTV3BenchmarkOperation : QueryBenchmarkOperation
    {
        protected readonly Container container;
        protected readonly Dictionary<string, object> sampleJObject;

        private readonly string databaseName;
        private readonly string containerName;

        protected bool initialized = false;

        protected readonly string partitionKeyPath;

        public abstract QueryDefinition QueryDefinition { get; }
        public abstract QueryRequestOptions QueryRequestOptions { get; }

        // Configurations
        public abstract bool IsCrossPartitioned { get; }
        public abstract bool IsPaginationEnabled { get; }
        public abstract bool IsQueryStream { get; }

        protected string executionItemId = null;
        protected string executionPartitionKey = null;

        public QueryTV3BenchmarkOperation(
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

        /// <summary>
        /// Generic implementation run any with 10 records inserted in prepareAsync() function
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public override async Task<OperationResult> ExecuteOnceAsync()
        {
            if (this.IsQueryStream)
            {
                return await this.ExecuteOnceAsyncWithStreams();
            }

            return await this.ExecuteOnceAsyncDefault();
        }

        private async Task<OperationResult> ExecuteOnceAsyncDefault()
        {
            if (this.IsPaginationEnabled)
            {
                return await this.ExecuteOnceAsyncWithPagination();
            }

            FeedIterator<Dictionary<string, object>> feedIterator = this.container.GetItemQueryIterator<Dictionary<string, object>>(
                        queryDefinition: this.QueryDefinition,
                        continuationToken: null,
                        requestOptions: this.QueryRequestOptions);

            double totalCharge = 0;
            CosmosDiagnostics lastDiagnostics = null;
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<Dictionary<string, object>> feedResponse = await feedIterator.ReadNextAsync();

                totalCharge += feedResponse.Headers.RequestCharge;
                lastDiagnostics = feedResponse.Diagnostics;

                if (feedResponse.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception($"QueryTV3BenchmarkOperation failed with {feedResponse?.StatusCode} " +
                        $"where pagination : {this.IsPaginationEnabled} and cross partition : {this.IsCrossPartitioned}");
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
                DatabseName = databaseName,
                ContainerName = containerName,
                RuCharges = totalCharge,
                CosmosDiagnostics = lastDiagnostics,
                LazyDiagnostics = () => lastDiagnostics?.ToString(),
            };
        }

        private async Task<OperationResult> ExecuteOnceAsyncWithPagination()
        {
            string continuationToken = null;
            double totalCharge = 0;
            CosmosDiagnostics lastDiagnostics = null;

            do
            {
                FeedIterator<Dictionary<string, object>> feedIterator = 
                    this.container.GetItemQueryIterator<Dictionary<string, object>>(
                        queryDefinition: this.QueryDefinition,
                        continuationToken: continuationToken,
                        requestOptions: this.QueryRequestOptions);

                FeedResponse<Dictionary<string, object>> feedResponse = await feedIterator.ReadNextAsync();

                if (feedResponse == null || feedResponse.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception($"QueryTV3BenchmarkOperation failed with {feedResponse?.StatusCode} " +
                        $"where pagination : {this.IsPaginationEnabled} and cross partition : {this.IsCrossPartitioned}");
                }

                foreach (Dictionary<string, object> item in feedResponse)
                {
                    // No-op check that forces any lazy logic to be executed
                    if (item == null)
                    {
                        throw new Exception("Null item was returned");
                    }
                }

                totalCharge += feedResponse.Headers.RequestCharge;
                lastDiagnostics = feedResponse.Diagnostics;

                continuationToken = feedResponse.ContinuationToken;

                if (!feedIterator.HasMoreResults)
                {
                    break;
                }

            } while (true);


            return new OperationResult()
            {
                DatabseName = databaseName,
                ContainerName = containerName,
                RuCharges = totalCharge,
                CosmosDiagnostics = lastDiagnostics,
                LazyDiagnostics = () => lastDiagnostics?.ToString(),
            };
        }

        private async Task<OperationResult> ExecuteOnceAsyncWithStreams()
        {
            if (this.IsPaginationEnabled)
            {
                return await this.ExecuteOnceAsyncWithStreamsAndPagination();
            }

            FeedIterator feedIterator = this.container.GetItemQueryStreamIterator(
            queryDefinition: this.QueryDefinition,
            continuationToken: null,
            requestOptions: this.QueryRequestOptions);

            double totalCharge = 0;
            CosmosDiagnostics lastDiagnostics = null;
            while (feedIterator.HasMoreResults)
            {
                using (ResponseMessage feedResponse = await feedIterator.ReadNextAsync())
                {
                    totalCharge += feedResponse.Headers.RequestCharge;
                    lastDiagnostics = feedResponse.Diagnostics;

                    if (feedResponse.StatusCode != HttpStatusCode.OK)
                    {
                        throw new Exception($"QueryTV3BenchmarkOperation failed with {feedResponse.StatusCode}");
                    }

                    // Access the stream to catch any lazy logic
                    using Stream stream = feedResponse.Content;
                }
            }

            return new OperationResult()
            {
                DatabseName = databaseName,
                ContainerName = containerName,
                RuCharges = totalCharge,
                CosmosDiagnostics = lastDiagnostics,
                LazyDiagnostics = () => lastDiagnostics?.ToString(),
            };
        }

        private async Task<OperationResult> ExecuteOnceAsyncWithStreamsAndPagination()
        {
            string continuationToken = null;
            double totalCharge = 0;
            CosmosDiagnostics lastDiagnostics = null;

            do
            {
                FeedIterator feedIterator =
                    this.container.GetItemQueryStreamIterator (
                        queryDefinition: this.QueryDefinition,
                        continuationToken: continuationToken,
                        requestOptions: this.QueryRequestOptions);

               ResponseMessage feedResponse = await feedIterator.ReadNextAsync();

                if (feedResponse == null || feedResponse.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception($"QueryTV3BenchmarkOperation failed with {feedResponse?.StatusCode} " +
                        $"where pagination : {this.IsPaginationEnabled} and cross partition : {this.IsCrossPartitioned}");
                }

                totalCharge += feedResponse.Headers.RequestCharge;
                lastDiagnostics = feedResponse.Diagnostics;

                continuationToken = feedResponse.ContinuationToken;

                if (!feedIterator.HasMoreResults)
                {
                    break;
                }

            } while (true);


            return new OperationResult()
            {
                DatabseName = databaseName,
                ContainerName = containerName,
                RuCharges = totalCharge,
                CosmosDiagnostics = lastDiagnostics,
                LazyDiagnostics = () => lastDiagnostics?.ToString(),
            };
        }


        /// <summary>
        /// Inserting 10 items which will be queried
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public override async Task PrepareAsync()
        {
            if (this.initialized)
            {
                return;
            }

            for (int itemCount = 0; itemCount < 100; itemCount++)
            {
                string objectId = Guid.NewGuid().ToString();

                // If single partitioned 
                if (this.IsCrossPartitioned || // Multi Partitioned
                    (!this.IsCrossPartitioned //Single Partitioned but partitionValue are not generated yet
                        && this.executionPartitionKey == null))
                {
                    this.executionItemId = objectId;
                    this.executionPartitionKey = Guid.NewGuid().ToString();
                }

                this.sampleJObject["id"] = objectId;
                this.sampleJObject[this.partitionKeyPath] = this.executionPartitionKey;

                using (MemoryStream inputStream = JsonHelper.ToStream(this.sampleJObject))
                {
                    using ResponseMessage itemResponse = await this.container.CreateItemStreamAsync(
                            inputStream,
                            new Microsoft.Azure.Cosmos.PartitionKey(this.executionPartitionKey));

                    System.Buffers.ArrayPool<byte>.Shared.Return(inputStream.GetBuffer());

                    if (itemResponse.StatusCode != HttpStatusCode.Created)
                    {
                        throw new Exception($"Create failed with statuscode: {itemResponse.StatusCode}");
                    }
                }
            }

            this.initialized = true;
        }
    }
}
