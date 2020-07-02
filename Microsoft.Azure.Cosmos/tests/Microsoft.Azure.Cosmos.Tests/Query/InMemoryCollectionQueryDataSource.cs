//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Partitions;

    internal sealed class InMemoryCollectionQueryDataSource : IQueryDataSource
    {
        private readonly InMemoryCollection inMemoryCollection;

        public InMemoryCollectionQueryDataSource(InMemoryCollection inMemoryCollection)
        {
            this.inMemoryCollection = inMemoryCollection ?? throw new ArgumentNullException(nameof(inMemoryCollection));
        }

        public Task<TryCatch<QueryPage>> ExecuteQueryAsync(
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            int partitionKeyRangeId,
            int pageSize,
            CancellationToken cancellationToken)
        {
            TryCatch<(List<InMemoryCollection.Record> records, long? resourceIdentifer)> tryExecuteQuery = this.inMemoryCollection.Query(
                sqlQuerySpec,
                partitionKeyRangeId,
                continuationToken != null ? long.Parse(continuationToken) : 0,
                pageSize);
            if (tryExecuteQuery.Failed)
            {
                return Task.FromResult(TryCatch<QueryPage>.FromException(tryExecuteQuery.Exception));
            }

            List<CosmosElement> cosmosElements = new List<CosmosElement>(tryExecuteQuery.Result.records.Count);
            foreach (InMemoryCollection.Record record in tryExecuteQuery.Result.records)
            {
                Dictionary<string, CosmosElement> keyValuePairs = new Dictionary<string, CosmosElement>
                {
                    ["_rid"] = CosmosNumber64.Create(record.ResourceIdentifier),
                    ["_ts"] = CosmosNumber64.Create(record.Timestamp),
                    ["id"] = CosmosString.Create(record.Identifier.ToString())
                };

                foreach (KeyValuePair<string, CosmosElement> property in record.Payload)
                {
                    keyValuePairs[property.Key] = property.Value;
                }

                cosmosElements.Add(CosmosObject.Create(keyValuePairs));
            }

            QueryPage queryPage = new QueryPage(
                documents: cosmosElements,
                requestCharge: 42,
                activityId: Guid.NewGuid().ToString(),
                responseLengthInBytes: 1337,
                cosmosQueryExecutionInfo: default,
                state: tryExecuteQuery.Result.resourceIdentifer.HasValue ? new QueryState(CosmosString.Create(tryExecuteQuery.Result.resourceIdentifer.Value.ToString())) : null);

            return Task.FromResult(TryCatch<QueryPage>.FromResult(queryPage));
        }
    }
}
