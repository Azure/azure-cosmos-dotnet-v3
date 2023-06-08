﻿//------------------------------------------------------------
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

    internal class ReadStreamExistsV2BenchmarkOperation : ReadBenchmarkOperation
    {
        private readonly string partitionKeyPath;
        private readonly Dictionary<string, object> sampleJObject;

        private readonly string databsaeName;
        private readonly string containerName;

        private string nextExecutionItemPartitionKey;
        private string nextExecutionItemId;

        private readonly DocumentClient documentClient;

        public ReadStreamExistsV2BenchmarkOperation(
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

        public override async Task<OperationResult> ExecuteOnceAsync()
        {
            Uri itemUri = UriFactory.CreateDocumentUri(this.databsaeName, this.containerName, this.nextExecutionItemId);
            ResourceResponse<Document> itemResponse = await this.documentClient.ReadDocumentAsync(
                        itemUri,
                        new RequestOptions() { PartitionKey = new PartitionKey(this.nextExecutionItemPartitionKey) }
                        );

            using (itemResponse.ResponseStream) { }
            double ruCharges = itemResponse.RequestCharge;

            return new OperationResult()
            {
                DatabseName = databsaeName,
                ContainerName = containerName,
                RuCharges = ruCharges,
                LazyDiagnostics = () => itemResponse.RequestDiagnosticsString,
            };
        }

        public override async Task PrepareAsync()
        {
            if (string.IsNullOrEmpty(this.nextExecutionItemId) ||
                string.IsNullOrEmpty(this.nextExecutionItemPartitionKey))
            {
                this.nextExecutionItemPartitionKey = Guid.NewGuid().ToString();
                this.nextExecutionItemId = Guid.NewGuid().ToString();

                this.sampleJObject["id"] = this.nextExecutionItemId;
                this.sampleJObject[this.partitionKeyPath] = this.nextExecutionItemPartitionKey;

                Uri collectionUri = UriFactory.CreateDocumentCollectionUri(this.databsaeName, this.containerName);
                using (MemoryStream inputStream = JsonHelper.ToStream(this.sampleJObject))
                {
                    ResourceResponse<Document> itemResponse = await this.documentClient.CreateDocumentAsync(
                            collectionUri,
                            this.sampleJObject,
                            new RequestOptions() { PartitionKey = new PartitionKey(this.nextExecutionItemPartitionKey) });

                    System.Buffers.ArrayPool<byte>.Shared.Return(inputStream.GetBuffer());

                    if (itemResponse.StatusCode != HttpStatusCode.Created)
                    {
                        throw new Exception($"Create failed with statuscode: {itemResponse.StatusCode}");
                    }
                }
            }
        }
    }
}
