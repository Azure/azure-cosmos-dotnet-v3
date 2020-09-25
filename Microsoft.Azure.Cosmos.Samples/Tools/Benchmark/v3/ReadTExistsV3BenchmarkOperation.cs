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
    using Newtonsoft.Json.Linq;

    internal class ReadTExistsV3BenchmarkOperation : IBenchmarkOperatrion
    {
        private readonly Container container;
        private readonly string partitionKeyPath;
        private readonly Dictionary<string, object> sampleJObject;

        private readonly string databsaeName;
        private readonly string containerName;

        private string nextExecutionItemPartitionKey;
        private string nextExecutionItemId;

        public ReadTExistsV3BenchmarkOperation(
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
        }

        public async Task<OperationResult> ExecuteOnceAsync()
        {
            ItemResponse<Dictionary<string, object>> itemResponse = await this.container.ReadItemAsync<Dictionary<string, object>>(
                        this.nextExecutionItemId,
                        new PartitionKey(this.nextExecutionItemPartitionKey));
            if (itemResponse.StatusCode != HttpStatusCode.OK)
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

        public async Task Prepare()
        {
            if (string.IsNullOrEmpty(this.nextExecutionItemId) ||
                string.IsNullOrEmpty(this.nextExecutionItemPartitionKey))
            {
                this.nextExecutionItemId = Guid.NewGuid().ToString();
                this.nextExecutionItemPartitionKey = Guid.NewGuid().ToString();

                this.sampleJObject["id"] = this.nextExecutionItemId;
                this.sampleJObject[this.partitionKeyPath] = this.nextExecutionItemPartitionKey;

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
