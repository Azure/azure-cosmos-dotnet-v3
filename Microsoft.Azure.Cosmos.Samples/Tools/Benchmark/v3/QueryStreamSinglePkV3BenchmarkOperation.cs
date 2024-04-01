//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{

    using Microsoft.Azure.Cosmos;

    internal class QueryStreamSinglePkV3BenchmarkOperation : QueryTV3BenchmarkOperation
    {
        public QueryStreamSinglePkV3BenchmarkOperation(
            CosmosClient cosmosClient,
            string dbName,
            string containerName,
            string partitionKeyPath,
            string sampleJson) : base(cosmosClient, dbName, containerName, partitionKeyPath, sampleJson)
        {
        }

        public override QueryDefinition QueryDefinition => new QueryDefinition("select * from T where T.id = @id")
                                                .WithParameter("@id", this.executionItemId);

        public override QueryRequestOptions QueryRequestOptions => new QueryRequestOptions()
        {
            PartitionKey = new PartitionKey(this.executionPartitionKey)
        };

        public override bool IsCrossPartitioned => false;

        public override bool IsPaginationEnabled => false;

        public override bool IsQueryStream => true;
    }
}
