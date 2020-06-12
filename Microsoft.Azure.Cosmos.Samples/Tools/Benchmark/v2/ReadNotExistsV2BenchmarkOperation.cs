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

    internal class ReadNotExistsV2BenchmarkOperation : IBenchmarkOperatrion
    {
        private readonly string databsaeName;
        private readonly string containerName;

        private string nextExecutionItemPartitionKey;
        private string nextExecutionItemId;

        private readonly DocumentClient documentClient;

        public ReadNotExistsV2BenchmarkOperation(
            DocumentClient documentClient,
            string dbName,
            string containerName,
            string partitionKeyPath,
            string sampleJson)
        {
            this.documentClient = documentClient;

            this.databsaeName = dbName;
            this.containerName = containerName;
        }

        public async Task<OperationResult> ExecuteOnceAsync()
        {
            Uri itemUri = UriFactory.CreateDocumentUri(this.databsaeName, this.containerName, this.nextExecutionItemId);

            try
            {
                ResourceResponse<Document> itemResponse = await this.documentClient.ReadDocumentAsync(
                        itemUri,
                        new Microsoft.Azure.Documents.Client.RequestOptions() { PartitionKey = new Microsoft.Azure.Documents.PartitionKey(this.nextExecutionItemPartitionKey) }
                        );

                throw new Exception($"Unexpected success {itemResponse.StatusCode} {itemResponse.RequestDiagnosticsString}");
            }
            catch(DocumentClientException dce)
            {
                if (dce.StatusCode != HttpStatusCode.NotFound)
                {
                    throw new Exception($"ReadItem failed wth {dce?.StatusCode} {dce?.ToString()}");
                }

                return new OperationResult()
                {
                    DatabseName = databsaeName,
                    ContainerName = containerName,
                    RuCharges = dce.RequestCharge,
                    lazyDiagnostics = () => dce.ToString(),
                };
            }
        }

        public Task Prepare()
        {
            if (string.IsNullOrEmpty(this.nextExecutionItemId) ||
                string.IsNullOrEmpty(this.nextExecutionItemPartitionKey))
            {
                this.nextExecutionItemPartitionKey = Guid.NewGuid().ToString();
                this.nextExecutionItemId = Guid.NewGuid().ToString();
            }

            return Task.CompletedTask;
        }
    }
}
