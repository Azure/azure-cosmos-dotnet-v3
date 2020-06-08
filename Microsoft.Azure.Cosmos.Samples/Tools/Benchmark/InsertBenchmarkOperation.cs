//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Newtonsoft.Json.Linq;

    internal class InsertV2BenchmarkOperation : IBenchmarkOperatrion
    {
        private readonly DocumentClient documentClient;
        private readonly Uri containerUri;

        private readonly string partitionKeyPath;
        private readonly Dictionary<string, object> sampleJObject;

        private readonly string databsaeName;
        private readonly string containerName;

        public InsertV2BenchmarkOperation(DocumentClient documentClient,
            string dbName,
            string containerName,
            string partitionKeyPath,
            string sampleJson)
        {
            this.documentClient = documentClient;
            this.containerUri = UriFactory.CreateDocumentCollectionUri(dbName, containerName);
            this.partitionKeyPath = partitionKeyPath.Replace("/", "");

            this.databsaeName = dbName;
            this.containerName = containerName;

            this.sampleJObject = JsonHelper.Deserialize<Dictionary<string, object>>(sampleJson);
        }

        public async Task<OperationResult> ExecuteOnceAsync()
        {
            ResourceResponse<Document> itemResponse = await this.documentClient.CreateDocumentAsync(
                    this.containerUri,
                    this.sampleJObject,
                    new Microsoft.Azure.Documents.Client.RequestOptions() { PartitionKey = new Microsoft.Azure.Documents.PartitionKey(this.sampleJObject[this.partitionKeyPath]) }
                    );

            Document value = itemResponse.Resource;

            double ruCharges = itemResponse.RequestCharge;
            return new OperationResult()
            {
                DatabseName = databsaeName,
                ContainerName = containerName,
                RuCharges = ruCharges,
                lazyDiagnostics = () => itemResponse.RequestDiagnosticsString,
            };
        }

        public Task Prepare()
        {
            string newPartitionKey = Guid.NewGuid().ToString();
            this.sampleJObject["id"] = Guid.NewGuid().ToString();
            this.sampleJObject[this.partitionKeyPath] = newPartitionKey;

            return Task.CompletedTask;
        }
    }


    internal class InsertBenchmarkOperation : IBenchmarkOperatrion
    {
        private readonly Container container;
        private readonly string partitionKeyPath;
        private readonly Dictionary<string, object> sampleJObject;

        private readonly string databsaeName;
        private readonly string containerName;

        public InsertBenchmarkOperation(
            Container container,
            string partitionKeyPath,
            string sampleJson)
        {
            this.container = container;
            this.partitionKeyPath = partitionKeyPath.Replace("/", "");

            this.databsaeName = container.Database.Id;
            this.containerName = container.Id;

            this.sampleJObject = JsonHelper.Deserialize<Dictionary<string, object>>(sampleJson);
        }

        public async Task<OperationResult> ExecuteOnceAsync()
        {
            ItemResponse<Dictionary<string, object>> itemResponse = await this.container.CreateItemAsync<Dictionary<string, object>>(
                    this.sampleJObject,
                    new Microsoft.Azure.Cosmos.PartitionKey(this.sampleJObject[this.partitionKeyPath].ToString()));

            double ruCharges = itemResponse.Headers.RequestCharge;
            return new OperationResult()
            {
                DatabseName = databsaeName,
                ContainerName = containerName,
                RuCharges = ruCharges,
                lazyDiagnostics = () => itemResponse.Diagnostics.ToString(),
            };
        }

        public Task Prepare()
        {
            string newPartitionKey = Guid.NewGuid().ToString();
            this.sampleJObject["id"] = Guid.NewGuid().ToString();
            this.sampleJObject[this.partitionKeyPath] = newPartitionKey;

            return Task.CompletedTask;
        }
    }
}
