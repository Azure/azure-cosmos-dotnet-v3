//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using Microsoft.Azure.Cosmos;

    internal class QueryStreamCrossPkOrderByWithPaginationV3BenchmarkOperation : QueryTV3BenchmarkOperation
    {
        public QueryStreamCrossPkOrderByWithPaginationV3BenchmarkOperation(
            CosmosClient cosmosClient,
            string dbName,
            string containerName,
            string partitionKeyPath,
            string sampleJson) : base(cosmosClient, dbName, containerName, partitionKeyPath, sampleJson)
        {
            this.IsQueryStream = true;
            this.IsPaginationEnabled = true;
            this.IsCrossPartitioned = true;
        }

        public override QueryDefinition QueryDefinition => new QueryDefinition("select * from T ORDER BY T.id");

        public override QueryRequestOptions QueryRequestOptions => new QueryRequestOptions()
        {
            MaxItemCount = 1
        };

    }
}





























