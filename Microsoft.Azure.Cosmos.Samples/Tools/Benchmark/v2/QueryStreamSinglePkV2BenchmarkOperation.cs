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

    internal class QueryStreamSinglePkV2BenchmarkOperation : QueryBenchmarkOperation
    {
        private readonly DocumentClient documentClient;
        private readonly string partitionKeyPath;
        private readonly Dictionary<string, object> sampleJObject;

        private readonly string databsaeName;
        private readonly string containerName;
        private readonly Uri containerUri;

        private readonly string executionItemPartitionKey;
        private readonly string executionItemId;
        private bool initialized = false;

        public QueryStreamSinglePkV2BenchmarkOperation(
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
            this.executionItemId = Guid.NewGuid().ToString();
            this.sampleJObject["id"] = this.executionItemId;
            this.sampleJObject[this.partitionKeyPath] = this.executionItemPartitionKey;
            this.containerUri = UriFactory.CreateDocumentCollectionUri(this.databsaeName, this.containerName);
        }

        public override async Task<OperationResult> ExecuteOnceAsync()
        {
            IDocumentQuery<dynamic> query = this.documentClient.CreateDocumentQuery<dynamic>(
                this.containerUri,
                new SqlQuerySpec("select * from T where T.id = @id")
                {
                    Parameters = new SqlParameterCollection()
                    {
                        new SqlParameter()
                        {
                            Name = "@id",
                            Value = this.executionItemId
                        }
                    }
                },
                new FeedOptions()
                {
                    PartitionKey = new PartitionKey(this.executionItemPartitionKey)
                }).AsDocumentQuery();

            double totalCharge = 0;
            Func<string> lastDiagnostics = null;
            while (query.HasMoreResults)
            {
                FeedResponse<dynamic> feedResponse = await query.ExecuteNextAsync();
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

        public override async Task PrepareAsync()
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
