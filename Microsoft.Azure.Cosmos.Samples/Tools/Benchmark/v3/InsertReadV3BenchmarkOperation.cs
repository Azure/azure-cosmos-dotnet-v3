//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    internal class InsertReadV3BenchmarkOperation : IBenchmarkOperation
    {
        private readonly CosmosClient client;
        private readonly Container container;
        private readonly string partitionKeyPath;
        private readonly Dictionary<string, object> sampleJObject;
        private readonly string databaseName;
        private readonly string containerName;

        public InsertReadV3BenchmarkOperation(
            CosmosClient cosmosClient,
            string dbName,
            string containerName,
            string partitionKeyPath,
            string sampleJson)
        {
            this.databaseName = dbName;
            this.containerName = containerName;
            this.client = cosmosClient;

            this.container = cosmosClient.GetContainer(this.databaseName, this.containerName);
            this.partitionKeyPath = partitionKeyPath.Replace("/", "");

            this.sampleJObject = JsonHelper.Deserialize<Dictionary<string, object>>(sampleJson);
        }

        public BenchmarkOperationType OperationType => BenchmarkOperationType.Insert;

        public async Task<OperationResult> ExecuteOnceAsync()
        {
            PartitionKey partitionKey = new PartitionKey(this.sampleJObject[this.partitionKeyPath].ToString());
            double ruCharges = 0;
            CosmosDiagnostics writeDiagnostics = null;
            using (MemoryStream input = JsonHelper.ToStream(this.sampleJObject))
            {
                ResponseMessage itemResponse = await this.container.CreateItemStreamAsync(
                        input,
                        partitionKey);

                ruCharges = itemResponse.Headers.RequestCharge;
                writeDiagnostics = itemResponse.Diagnostics;
                System.Buffers.ArrayPool<byte>.Shared.Return(input.GetBuffer());
                
                if (!itemResponse.IsSuccessStatusCode)
                {
                    return new OperationResult()
                    {
                        DatabseName = this.databaseName,
                        ContainerName = this.containerName,
                        OperationType = this.OperationType,
                        RuCharges = ruCharges,
                        CosmosDiagnostics = itemResponse.Diagnostics,
                        LazyDiagnostics = () => itemResponse.Diagnostics.ToString(),
                    };
                }
            }

            using ResponseMessage readResponse = await this.container.ReadItemStreamAsync(
                    this.sampleJObject["id"].ToString(),
                    partitionKey);
            ruCharges += readResponse.Headers.RequestCharge;

            return new OperationResult()
            {
                DatabseName = this.databaseName,
                ContainerName = this.containerName,
                OperationType = this.OperationType,
                RuCharges = ruCharges,
                CosmosDiagnostics = readResponse.Diagnostics,
                LazyDiagnostics = () => $"read {readResponse.Diagnostics} write: {writeDiagnostics}",
            };
        }

        public Task PrepareAsync()
        {
            string newPartitionKey = Guid.NewGuid().ToString();
            this.sampleJObject["id"] = Guid.NewGuid().ToString();
            this.sampleJObject[this.partitionKeyPath] = newPartitionKey;
            return Task.CompletedTask;
        }
    }
}
