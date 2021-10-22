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

    internal abstract class QueryTSimpleV3BenchmarkOperation : IBenchmarkOperation
    {
        private readonly Container container;
        private readonly Dictionary<string, object> sampleJObject;

        private readonly string databaseName;
        private readonly string containerName;

        private bool initialized = false;

        protected readonly string partitionKeyPath;

        public abstract string ExecutionItemPartitionKey { get; }
        public abstract QueryDefinition QueryDefinition { get; }
        public abstract QueryRequestOptions QueryRequestOptions { get; }
        public abstract IDictionary<string, string> ObjectProperties { get; }

        public QueryTSimpleV3BenchmarkOperation(
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

            foreach (KeyValuePair<string, string> kvPair in this.ObjectProperties)
            {
                this.sampleJObject[kvPair.Key] = kvPair.Value;
            }
        }

        public async Task<OperationResult> ExecuteOnceAsync()
        {
            Console.WriteLine(this.QueryDefinition.QueryText);
            Console.WriteLine(this.QueryRequestOptions.PartitionKey.Value);
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

        public async Task PrepareAsync()
        {
            if (this.initialized)
            {
                return;
            }

            Console.WriteLine("id : " + this.sampleJObject["id"]);
            Console.WriteLine("this.partitionKeyPath : " + this.sampleJObject[this.partitionKeyPath]);
            
            using (MemoryStream inputStream = JsonHelper.ToStream(this.sampleJObject))
            {
                using ResponseMessage itemResponse = await this.container.CreateItemStreamAsync(
                        inputStream,
                        new Microsoft.Azure.Cosmos.PartitionKey(this.ExecutionItemPartitionKey));

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
