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

    internal class InsertBenchmarkOperation : IBenchmarkOperatrion
    {
        private readonly Container container;
        private readonly string partitionKeyPath;
        private readonly Dictionary<string, object> sampleJObject;

        private readonly string databsaeName;
        private readonly string containerName;

        private Stream nextExecutionItemPayload;
        private string nextExecutionItemPartitionKey;

        public InsertBenchmarkOperation(
            Container container,
            string partitionKeyPath,
            string sampleJson)
        {
            this.container = container;
            this.partitionKeyPath = partitionKeyPath.Replace("/", "");

            this.databsaeName = container.Database.Id;
            this.containerName = container.Id;

            this.sampleJObject = JsonHelper.Deserialize<Dictionary<string, object>>(sampleJson);
        }

        public async Task<OperationResult> ExecuteOnceAsync()
        {
            using (Stream inputStream = this.nextExecutionItemPayload)
            {
                ResponseMessage itemResponse = await this.container.CreateItemStreamAsync(
                        inputStream,
                        new PartitionKey(this.nextExecutionItemPartitionKey));

                double ruCharges = itemResponse.Headers.RequestCharge;
                return new OperationResult()
                {
                    DatabseName = databsaeName,
                    ContainerName = containerName,
                    RuCharges = ruCharges,
                    lazyDiagnostics = () => itemResponse.Diagnostics.ToString(),
                };
            }
        }

        public void Prepare()
        {
            string newPartitionKey = Guid.NewGuid().ToString();
            this.sampleJObject["id"] = Guid.NewGuid().ToString();
            this.sampleJObject[this.partitionKeyPath] = newPartitionKey;

            this.nextExecutionItemPayload = JsonHelper.ToStream(this.sampleJObject);
            this.nextExecutionItemPartitionKey = newPartitionKey;
        }
    }
}
