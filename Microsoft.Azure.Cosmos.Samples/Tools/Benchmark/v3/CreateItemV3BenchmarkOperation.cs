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

    internal class CreateItemV3BenchmarkOperation : IBenchmarkOperation
    {
        private readonly Container container;
        private readonly string partitionKeyPath;
        private readonly Dictionary<string, object> sampleJObject;
        private readonly string databaseName;
        private readonly string containerName;

        public CreateItemV3BenchmarkOperation(
            CosmosClient cosmosClient,
            string dbName,
            string containerName,
            string partitionKeyPath,
            string sampleJson)
        {
            this.databaseName = dbName;
            this.containerName = containerName;
            this.container = cosmosClient.GetContainer(this.databaseName, this.containerName);
            this.partitionKeyPath = partitionKeyPath.Replace("/", "");
            this.sampleJObject = JsonHelper.Deserialize<Dictionary<string, object>>(sampleJson);
        }

        public BenchmarkOperationType OperationType => BenchmarkOperationType.Insert;

        public Task PrepareAsync()
        {
            this.sampleJObject["id"] = Guid.NewGuid().ToString();
            this.sampleJObject[this.partitionKeyPath] = Guid.NewGuid().ToString();
            return Task.CompletedTask;
        }

        public async Task<OperationResult> ExecuteOnceAsync()
        {
            PartitionKey partitionKey = new PartitionKey(this.sampleJObject[this.partitionKeyPath].ToString());
            ItemResponse<Dictionary<string, object>> response = await this.container.CreateItemAsync(this.sampleJObject, partitionKey: partitionKey);

            return new OperationResult
            {
                DatabseName = this.databaseName,
                ContainerName = this.containerName,
                OperationType = this.OperationType,
                RuCharges = response.RequestCharge,
                CosmosDiagnostics = response.Diagnostics,
                LazyDiagnostics = () => response.Diagnostics.ToString(),
            };
        }
    }
}