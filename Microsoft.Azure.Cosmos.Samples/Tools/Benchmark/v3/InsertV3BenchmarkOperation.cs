//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;

    internal class InsertV3BenchmarkOperation : IBenchmarkOperatrion
    {
        private readonly Container container;
        private readonly string partitionKeyPath;
        private readonly Dictionary<string, object> sampleJObject;

        private readonly string databsaeName;
        private readonly string containerName;

        public InsertV3BenchmarkOperation(
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
            ItemResponse<Dictionary<string, object>> itemResponse = await this.container.CreateItemAsync<Dictionary<string, object>>(
                    this.sampleJObject,
                    new PartitionKey(this.sampleJObject[this.partitionKeyPath].ToString()));

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
