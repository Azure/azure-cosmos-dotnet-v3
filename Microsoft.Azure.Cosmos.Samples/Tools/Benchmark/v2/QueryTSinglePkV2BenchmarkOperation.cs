//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Linq;

    internal class QueryTSinglePkV2BenchmarkOperation : IBenchmarkOperation
    {
        private readonly DocumentClient documentClient;
        private readonly string partitionKeyPath;
        private readonly Dictionary<string, object> sampleJObject;

        private readonly string databsaeName;
        private readonly string containerName;

        private readonly string executionItemPartitionKey;
        private readonly string executionItemId;
        private readonly SqlQuerySpec queryDefinition;
        private readonly FeedOptions queryRequestOptions;
        private bool initialized = false;

        public QueryTSinglePkV2BenchmarkOperation(
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
            this.executionItemPartitionKey = Guid.NewGuid().ToString();
            this.queryRequestOptions = new FeedOptions() { PartitionKey = new PartitionKey(this.executionItemPartitionKey) };
            this.executionItemId = Guid.NewGuid().ToString();
            this.sampleJObject["id"] = this.executionItemId;
            this.sampleJObject[this.partitionKeyPath] = this.executionItemPartitionKey;

            this.queryDefinition = new SqlQuerySpec("select * from T where T.id = @id")
            {
                Parameters = new SqlParameterCollection()
                {
                    new SqlParameter()
                    {
                        Name = "@id",
                        Value = this.executionItemId
                    }
                }
            };
        }

        public async Task<OperationResult> ExecuteOnceAsync()
        {
            Uri containerUri = UriFactory.CreateDocumentCollectionUri(this.databsaeName, this.containerName);
            IDocumentQuery<Dictionary<string, object>> query = this.documentClient.CreateDocumentQuery<Dictionary<string, object>>(
                containerUri,
                this.queryDefinition,
                this.queryRequestOptions).AsDocumentQuery();

            double totalCharge = 0;
            Func<string> lastDiagnostics = null;
            while (query.HasMoreResults)
            {
                FeedResponse<Dictionary<string, object>> feedResponse = await query.ExecuteNextAsync<Dictionary<string, object>>();
                foreach(Dictionary<string, object> item in feedResponse)
                {
                    if(item == null)
                    {
                        throw new Exception("Null item was returned");
                    }
                }

                totalCharge += feedResponse.RequestCharge;
                lastDiagnostics = () => feedResponse.RequestDiagnosticsString;
            }

            return new OperationResult()
            {
                DatabseName = databsaeName,
                ContainerName = containerName,
                RuCharges = totalCharge,
                LazyDiagnostics = lastDiagnostics,
            };
        }

        public async Task PrepareAsync()
        {
            if (this.initialized)
            {
                return;
            }

            Uri containerUri = UriFactory.CreateDocumentCollectionUri(this.databsaeName, this.containerName);
            ResourceResponse<Document> itemResponse = await this.documentClient.CreateDocumentAsync(
                    containerUri,
                    this.sampleJObject,
                    new RequestOptions() { PartitionKey = new PartitionKey(this.executionItemPartitionKey) });
            if (itemResponse.StatusCode != HttpStatusCode.Created)
            {
                throw new Exception($"Create failed with statuscode: {itemResponse.StatusCode}");
            }

            this.initialized = true;
        }
    }
}
