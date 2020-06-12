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
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;

    internal class ReadFeedStreamV2BenchmarkOperation : IBenchmarkOperatrion
    {
        private readonly DocumentClient documentClient;
        private readonly string partitionKeyPath;
        private readonly Dictionary<string, object> sampleJObject;

        private readonly string databsaeName;
        private readonly string containerName;

        private string nextExecutionItemPartitionKey;
        private string nextExecutionItemId;

        public ReadFeedStreamV2BenchmarkOperation(
            DocumentClient documentClient,
            string dbName,
            string containerName,
            string partitionKeyPath,
            string sampleJson)
        {
            this.databsaeName = dbName;
            this.containerName = containerName;

            this.documentClient = documentClient;
            this.partitionKeyPath = partitionKeyPath.Replace("/", "");

            this.sampleJObject = JsonHelper.Deserialize<Dictionary<string, object>>(sampleJson);
        }

        public async Task<OperationResult> ExecuteOnceAsync()
        {
            Uri containerUri = UriFactory.CreateDocumentCollectionUri(this.databsaeName, this.containerName);
            FeedResponse<dynamic> feedResponse = await this.documentClient.ReadDocumentFeedAsync(
                containerUri,
                new FeedOptions() { PartitionKey = new PartitionKey(this.nextExecutionItemPartitionKey) });

            double ruCharges = feedResponse.RequestCharge;
            return new OperationResult()
            {
                DatabseName = databsaeName,
                ContainerName = containerName,
                RuCharges = ruCharges,
                lazyDiagnostics = () => feedResponse.QueryMetrics.ToString(),
            };
        }

        public async Task Prepare()
        {
            if (string.IsNullOrEmpty(this.nextExecutionItemId) ||
                string.IsNullOrEmpty(this.nextExecutionItemPartitionKey))
            {
                this.nextExecutionItemId = Guid.NewGuid().ToString();
                this.nextExecutionItemPartitionKey = Guid.NewGuid().ToString();

                this.sampleJObject["id"] = this.nextExecutionItemId;
                this.sampleJObject[this.partitionKeyPath] = this.nextExecutionItemPartitionKey;

                Uri containerUri = UriFactory.CreateDocumentCollectionUri(this.databsaeName, this.containerName);
                ResourceResponse<Document> itemResponse = await this.documentClient.CreateDocumentAsync(
                        containerUri,
                        this.sampleJObject,
                        new RequestOptions() { PartitionKey = new PartitionKey(this.nextExecutionItemPartitionKey) });
                if (itemResponse.StatusCode != HttpStatusCode.Created)
                {
                    throw new Exception($"Create failed with statuscode: {itemResponse.StatusCode}");
                }
            }
        }
    }
}
