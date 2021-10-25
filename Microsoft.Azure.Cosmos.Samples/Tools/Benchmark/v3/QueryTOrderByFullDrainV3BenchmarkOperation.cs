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

        public override bool IsCrossPartitioned => false;

        public override QueryDefinition QueryDefinition => new QueryDefinition("select * from T ORDER BY T.id");

        public override QueryRequestOptions QueryRequestOptions => new QueryRequestOptions()
        {
            MaxItemCount = 1,
            PartitionKey = new PartitionKey(this.executionPartitionKey)
        };
    }
}
