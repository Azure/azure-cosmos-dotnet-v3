//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using Microsoft.Azure.Cosmos;

    internal class QueryTSinglePkOrderByWithPaginationV3BenchmarkOperation : QueryTV3BenchmarkOperation
    {
        public QueryTSinglePkOrderByWithPaginationV3BenchmarkOperation(
            CosmosClient cosmosClient,
            string dbName,
            string containerName,
            string partitionKeyPath,
            string sampleJson) : base(cosmosClient, dbName, containerName, partitionKeyPath, sampleJson)
        {
            this.IsPaginationEnabled = true;
        }

        public override QueryDefinition QueryDefinition => new QueryDefinition("select * from T ORDER BY T.id");

        public override QueryRequestOptions QueryRequestOptions => new QueryRequestOptions()
        {
            MaxItemCount = 1,
            PartitionKey = new PartitionKey(this.executionPartitionKey)
        };

    }
}





























