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

    internal class QueryTOrderByCTV3BenchmarkOperation : IBenchmarkOperation
    {
        protected readonly Container container;
        protected readonly Dictionary<string, object> sampleJObject;

        private readonly string databaseName;
        private readonly string containerName;

        protected bool initialized = false;

        protected readonly string partitionKeyPath;

        protected readonly string executionItemPartitionKey = Guid.NewGuid().ToString();
        protected readonly string executionItemId = Guid.NewGuid().ToString();

        public QueryTOrderByCTV3BenchmarkOperation(
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

        public virtual async Task<OperationResult> ExecuteOnceAsync()
        {
            string continuationToken = null;
            double totalCharge = 0;
            CosmosDiagnostics lastDiagnostics = null;

            do
            {
                FeedIterator<Dictionary<string, object>> feedIterator = this.container.GetItemQueryIterator<Dictionary<string, object>>(
                    queryDefinition: new QueryDefinition("select * from T ORDER BY T.id"),
                    continuationToken: continuationToken,
                    requestOptions: new QueryRequestOptions()
                    {
                        MaxItemCount = 1
                    });

                FeedResponse<Dictionary<string, object>> feedResponse = await feedIterator.ReadNextAsync();
                totalCharge += feedResponse.Headers.RequestCharge;
                lastDiagnostics = feedResponse.Diagnostics;
                continuationToken = feedResponse.ContinuationToken;

                if (feedResponse.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception($"QuerySinglePkStreamV3BenchmarkOperation failed with {feedResponse.StatusCode}");
                }

                foreach (Dictionary<string, object> item in feedResponse)
                {
                    // No-op check that forces any lazy logic to be executed
                    if (item == null)
                    {
                        throw new Exception("Null item was returned");
                    }
                }

                if(!feedIterator.HasMoreResults)
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

        public virtual async Task PrepareAsync()
        {
            if (this.initialized)
            {
                return;
            }
            for (int itemCount = 0; itemCount < 3; itemCount++)
            {
                this.sampleJObject["id"] = Guid.NewGuid().ToString();

                string partitionValue = Guid.NewGuid().ToString();
                this.sampleJObject[this.partitionKeyPath] = partitionValue;

                using (MemoryStream inputStream = JsonHelper.ToStream(this.sampleJObject))
                {
                    using ResponseMessage itemResponse = await this.container.CreateItemStreamAsync(
                            inputStream,
                            new Microsoft.Azure.Cosmos.PartitionKey(partitionValue));

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





























