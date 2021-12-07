//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using Microsoft.Azure.Cosmos;

    internal class QueryStreamCrossPkDistinctFullDrainV3BenchmarkOperation : QueryTV3BenchmarkOperation
    {
        public QueryStreamCrossPkDistinctFullDrainV3BenchmarkOperation(
            CosmosClient cosmosClient,
            string dbName,
            string containerName,
            string partitionKeyPath,
            string sampleJson) : base(cosmosClient, dbName, containerName, partitionKeyPath, sampleJson)
        {
        }

        public override QueryDefinition QueryDefinition => new QueryDefinition("SELECT DISTINCT * FROM c");

        public override QueryRequestOptions QueryRequestOptions => new QueryRequestOptions()
        {
            MaxItemCount = 1
        };

        public override bool IsCrossPartitioned => true;

        public override bool IsPaginationEnabled => false;

        public override bool IsQueryStream => true;
    }
}
