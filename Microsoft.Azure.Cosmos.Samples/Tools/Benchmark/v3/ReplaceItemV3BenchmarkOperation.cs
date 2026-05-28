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

    internal class ReplaceItemV3BenchmarkOperation : IBenchmarkOperation
    {
        private readonly Container container;
        private readonly string partitionKeyPath;
        private readonly Dictionary<string, object> sampleJObject;
        private readonly string databaseName;
        private readonly string containerName;
        private string itemId;
        private string itemPk;

        public ReplaceItemV3BenchmarkOperation(
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

        public BenchmarkOperationType OperationType => BenchmarkOperationType.Read;

        public async Task PrepareAsync()
        {
            this.itemId = Guid.NewGuid().ToString();
            this.itemPk = Guid.NewGuid().ToString();
            this.sampleJObject["id"] = this.itemId;
            this.sampleJObject[this.partitionKeyPath] = this.itemPk;
            this.sampleJObject["other"] = "Original";

            using (MemoryStream input = JsonHelper.ToStream(this.sampleJObject))
            {
                ResponseMessage itemResponse = await this.container.CreateItemStreamAsync(input, new PartitionKey(this.itemPk));
                System.Buffers.ArrayPool<byte>.Shared.Return(input.GetBuffer());
            }
        }

        public async Task<OperationResult> ExecuteOnceAsync()
        {
            this.sampleJObject["other"] = "Updated Other";
            ItemResponse<Dictionary<string, object>> response = await this.container.ReplaceItemAsync(this.sampleJObject, this.itemId, new PartitionKey(this.itemPk));
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
