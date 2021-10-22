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

    internal class QueryTOrderByFullDrainV3BenchmarkOperation : QueryTV3BenchmarkOperation
    {

        public QueryTOrderByFullDrainV3BenchmarkOperation(
            CosmosClient cosmosClient,
            string dbName,
            string containerName,
            string partitionKeyPath,
            string sampleJson) : base(cosmosClient, dbName, containerName, partitionKeyPath, sampleJson) 
        {
        }

        public override QueryDefinition QueryDefinition => new QueryDefinition("select * from T ORDER BY T.id");

        public override QueryRequestOptions QueryRequestOptions => new QueryRequestOptions()
        {
            MaxItemCount = 1
        };

        public override IDictionary<string, string> ObjectProperties => null;

        public override async Task PrepareAsync()
        {
            if (this.initialized)
            {
                return;
            }
            for(int itemCount = 0; itemCount < 3; itemCount++)
            {
                this.sampleJObject["id"] = Guid.NewGuid().ToString();

                string partitioValue = Guid.NewGuid().ToString();
                this.sampleJObject[this.partitionKeyPath] = partitioValue;

                using (MemoryStream inputStream = JsonHelper.ToStream(this.sampleJObject))
                {
                    using ResponseMessage itemResponse = await this.container.CreateItemStreamAsync(
                            inputStream,
                            new Microsoft.Azure.Cosmos.PartitionKey(partitioValue));

                    System.Buffers.ArrayPool<byte>.Shared.Return(inputStream.GetBuffer());

                    if (itemResponse.StatusCode != HttpStatusCode.Created)
                    {
                        throw new Exception($"Create failed with statuscode: {itemResponse.StatusCode}");
                    }
                }
            }

            this.initialized = true;
        }
    }
}
