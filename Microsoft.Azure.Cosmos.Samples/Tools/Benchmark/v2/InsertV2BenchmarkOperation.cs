//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;

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
                    new RequestOptions() { PartitionKey = new PartitionKey(this.sampleJObject[this.partitionKeyPath]) }
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
}
