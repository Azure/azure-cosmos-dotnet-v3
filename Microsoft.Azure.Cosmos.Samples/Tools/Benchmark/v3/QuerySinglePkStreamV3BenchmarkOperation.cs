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

    internal class QuerySinglePkStreamV3BenchmarkOperation : IBenchmarkOperation
    {
        private readonly Container container;
        private readonly string partitionKeyPath;
        private readonly Dictionary<string, object> sampleJObject;

        private readonly string databsaeName;
        private readonly string containerName;

        private readonly string executionItemPartitionKey;
        private readonly string executionItemId;
        private readonly QueryDefinition queryDefinition;
        private readonly QueryRequestOptions queryRequestOptions;
        private bool initialized = false;

        public QuerySinglePkStreamV3BenchmarkOperation(
            CosmosClient cosmosClient,
            string dbName,
            string containerName,
            string partitionKeyPath,
            string sampleJson)
        {
            this.databsaeName = dbName;
            this.containerName = containerName;

            this.container = cosmosClient.GetContainer(this.databsaeName, this.containerName);
            this.partitionKeyPath = partitionKeyPath.Replace("/", "");

            this.sampleJObject = JsonHelper.Deserialize<Dictionary<string, object>>(sampleJson);
            this.executionItemPartitionKey = Guid.NewGuid().ToString();
            this.queryRequestOptions = new QueryRequestOptions() { PartitionKey = new PartitionKey(this.executionItemPartitionKey) };
            this.executionItemId = Guid.NewGuid().ToString();
            this.sampleJObject["id"] = this.executionItemId;
            this.sampleJObject[this.partitionKeyPath] = this.executionItemPartitionKey;

            this.queryDefinition = new QueryDefinition("select * from T where T.id = @id").WithParameter("@id", this.executionItemId);
        }

        public async Task<OperationResult> ExecuteOnceAsync()
        {
            FeedIterator feedIterator = this.container
                .GetItemQueryStreamIterator(
                        queryDefinition: this.queryDefinition,
                        continuationToken: null,
                        requestOptions: this.queryRequestOptions);

            double totalCharge = 0;
            CosmosDiagnostics lastDiagnostics = null;
            while (feedIterator.HasMoreResults)
            {
                ResponseMessage feedResponse = await feedIterator.ReadNextAsync();
                totalCharge += feedResponse.Headers.RequestCharge;
                lastDiagnostics = feedResponse.Diagnostics;
                if (feedResponse.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception($"QuerySinglePkStreamV3BenchmarkOperation failed with {feedResponse.StatusCode}");
                }
            }
            
            return new OperationResult()
            {
                DatabseName = databsaeName,
                ContainerName = containerName,
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

            using (MemoryStream inputStream = JsonHelper.ToStream(this.sampleJObject))
            {
                using ResponseMessage itemResponse = await this.container.CreateItemStreamAsync(
                        inputStream,
                        new Microsoft.Azure.Cosmos.PartitionKey(this.executionItemPartitionKey));

                System.Buffers.ArrayPool<byte>.Shared.Return(inputStream.GetBuffer());

                if (itemResponse.StatusCode != HttpStatusCode.Created)
                {
                    throw new Exception($"Create failed with statuscode: {itemResponse.StatusCode}");
                }
            }

            this.initialized = true;
        }
    }
}
