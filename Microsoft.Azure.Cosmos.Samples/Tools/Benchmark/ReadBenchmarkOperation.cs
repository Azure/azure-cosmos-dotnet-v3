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
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;

    internal class ReadV2BenchmarkOperation : IBenchmarkOperatrion
    {
        private readonly string partitionKeyPath;
        private readonly Dictionary<string, object> sampleJObject;

        private readonly string databsaeName;
        private readonly string containerName;

        private string nextExecutionItemPartitionKey;
        private string nextExecutionItemId;

        private readonly DocumentClient documentClient;

        public ReadV2BenchmarkOperation(
            DocumentClient documentClient,
            string dbName,
            string containerName,
            string partitionKeyPath,
            string sampleJson)
        {
            this.partitionKeyPath = partitionKeyPath.Replace("/", "");
            this.documentClient = documentClient;

            this.databsaeName = dbName;
            this.containerName = containerName;

            this.sampleJObject = JsonHelper.Deserialize<Dictionary<string, object>>(sampleJson);
        }

        public async Task<OperationResult> ExecuteOnceAsync()
        {
            Uri itemUri = UriFactory.CreateDocumentUri(this.databsaeName, this.containerName, this.nextExecutionItemId);
            ResourceResponse<Document> itemResponse = await this.documentClient.ReadDocumentAsync(
                    itemUri,
                    new Microsoft.Azure.Documents.Client.RequestOptions() { PartitionKey = new Microsoft.Azure.Documents.PartitionKey(this.nextExecutionItemPartitionKey) }
                    );


            if (itemResponse.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception($"ReadItem unexpected exception with {itemResponse.StatusCode}");
            }

            double ruCharges = itemResponse.RequestCharge;
            return new OperationResult()
            {
                DatabseName = databsaeName,
                ContainerName = containerName,
                RuCharges = ruCharges,
                lazyDiagnostics = () => itemResponse.RequestDiagnosticsString,
            };
        }

        public async Task Prepare()
        {
            if (string.IsNullOrEmpty(this.nextExecutionItemId) ||
                string.IsNullOrEmpty(this.nextExecutionItemPartitionKey))
            {
                this.nextExecutionItemPartitionKey = Guid.NewGuid().ToString();
                this.nextExecutionItemId = Guid.NewGuid().ToString();
                this.sampleJObject["id"] = this.nextExecutionItemId;
                this.sampleJObject[this.partitionKeyPath] = this.nextExecutionItemPartitionKey;

                Uri collectionUri = UriFactory.CreateDocumentCollectionUri(this.databsaeName, this.containerName);
                using (Stream inputStream = JsonHelper.ToStream(this.sampleJObject))
                {
                    ResourceResponse<Document> itemResponse = await this.documentClient.CreateDocumentAsync(
                            collectionUri,
                            this.sampleJObject,
                            new Microsoft.Azure.Documents.Client.RequestOptions() { PartitionKey = new Microsoft.Azure.Documents.PartitionKey(this.nextExecutionItemPartitionKey) }
                            );
                    if (itemResponse.StatusCode != HttpStatusCode.Created)
                    {
                        throw new Exception($"Create failed with statuscode: {itemResponse.StatusCode}");
                    }
                }
            }
        }
    }

    internal class ReadBenchmarkOperation : IBenchmarkOperatrion
    {
        private readonly Container container;
        private readonly string partitionKeyPath;
        private readonly Dictionary<string, object> sampleJObject;

        private readonly string databsaeName;
        private readonly string containerName;

        private string nextExecutionItemPartitionKey;
        private string nextExecutionItemId;

        public ReadBenchmarkOperation(
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
            using (ResponseMessage itemResponse = await this.container.ReadItemStreamAsync(
                        this.nextExecutionItemId,
                        new Microsoft.Azure.Cosmos.PartitionKey(this.nextExecutionItemPartitionKey)))
            {
                if (itemResponse.StatusCode != HttpStatusCode.NotFound)
                {
                    throw new Exception($"ReadItem failed wth {itemResponse.StatusCode}");
                }
                
                double ruCharges = itemResponse.Headers.RequestCharge;
                return new OperationResult()
                {
                    DatabseName = databsaeName,
                    ContainerName = containerName,
                    RuCharges = ruCharges,
                    lazyDiagnostics = () => itemResponse.Diagnostics.ToString(),
                };
            }
        }

        public async Task Prepare()
        {
            if (string.IsNullOrEmpty(this.nextExecutionItemId) ||
                string.IsNullOrEmpty(this.nextExecutionItemPartitionKey))
            {
                string newPartitionKey = Guid.NewGuid().ToString();
                this.sampleJObject["id"] = Guid.NewGuid().ToString();
                this.sampleJObject[this.partitionKeyPath] = newPartitionKey;

                this.nextExecutionItemId = newPartitionKey;
                this.nextExecutionItemPartitionKey = newPartitionKey;

                using (Stream inputStream = JsonHelper.ToStream(this.sampleJObject))
                {
                    ResponseMessage itemResponse = await this.container.CreateItemStreamAsync(
                            inputStream,
                            new Microsoft.Azure.Cosmos.PartitionKey(this.nextExecutionItemPartitionKey));
                    if (itemResponse.StatusCode != HttpStatusCode.Created)
                    {
                        throw new Exception($"Create failed with statuscode: {itemResponse.StatusCode}");
                    }
                }
            }
        }
    }
}
