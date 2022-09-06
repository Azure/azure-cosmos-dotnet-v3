//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using Microsoft.Azure.Cosmos;

    internal class QueryTCrossPkV3BenchmarkOperation : QueryTV3BenchmarkOperation
    {
        public QueryTCrossPkV3BenchmarkOperation(
            CosmosClient cosmosClient,
            string dbName,
            string containerName,
            string partitionKeyPath,
            string sampleJson) : base(cosmosClient, dbName, containerName, partitionKeyPath, sampleJson)
        {
        }

        public override QueryDefinition QueryDefinition => new QueryDefinition("select * from T where T.id = @id")
                                                .WithParameter("@id", this.executionItemId);

        public override QueryRequestOptions QueryRequestOptions => null;

        public override bool IsCrossPartitioned => true;

        public override bool IsPaginationEnabled => false;

        public override bool IsQueryStream => false;
    }
}
