// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.GroupBy
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Aggregate;

    internal abstract partial class GroupByQueryPipelineStage : QueryPipelineStageBase
    {
        private sealed class ClientGroupByQueryPipelineStage : GroupByQueryPipelineStage
        {
            public const string ContinuationTokenNotSupportedWithGroupBy = "Continuation token is not supported for queries with GROUP BY. Do not use FeedResponse.ResponseContinuation or remove the GROUP BY from the query.";

            private ClientGroupByQueryPipelineStage(
                IQueryPipelineStage source,
                CancellationToken cancellationToken,
                GroupingTable groupingTable)
                : base(source, cancellationToken, groupingTable)
            {
            }

            public static TryCatch<IQueryPipelineStage> MonadicCreate(
                CosmosElement requestContinuation,
                CancellationToken cancellationToken,
                MonadicCreatePipelineStage monadicCreatePipelineStage,
                IReadOnlyDictionary<string, AggregateOperator?> groupByAliasToAggregateType,
                IReadOnlyList<string> orderedAliases,
                bool hasSelectValue)
            {
                TryCatch<GroupingTable> tryCreateGroupingTable = GroupingTable.TryCreateFromContinuationToken(
                    groupByAliasToAggregateType,
                    orderedAliases,
                    hasSelectValue,
                    continuationToken: null);

                if (tryCreateGroupingTable.Failed)
                {
                    return TryCatch<IQueryPipelineStage>.FromException(tryCreateGroupingTable.Exception);
                }

                TryCatch<IQueryPipelineStage> tryCreateSource = monadicCreatePipelineStage(requestContinuation, cancellationToken);
                if (tryCreateSource.Failed)
                {
                    return tryCreateSource;
                }

                IQueryPipelineStage stage = new ClientGroupByQueryPipelineStage(
                    tryCreateSource.Result,
                    cancellationToken,
                    tryCreateGroupingTable.Result);

                return TryCatch<IQueryPipelineStage>.FromResult(stage);
            }

            protected override async Task<TryCatch<QueryPage>> GetNextPageAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Draining GROUP BY is broken down into two stages:

                double requestCharge = 0.0;
                long responseLengthInBytes = 0;

                int maxPageSize = 0;

                while (await this.inputStage.MoveNextAsync())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Stage 1: 
                    // Drain the groupings fully from all continuation and all partitions
                    TryCatch<QueryPage> tryGetSourcePage = this.inputStage.Current;
                    if (tryGetSourcePage.Failed)
                    {
                        return tryGetSourcePage;
                    }

                    QueryPage sourcePage = tryGetSourcePage.Result;

                    requestCharge += sourcePage.RequestCharge;
                    responseLengthInBytes += sourcePage.ResponseLengthInBytes;
                    maxPageSize = Math.Max(sourcePage.Documents.Count, maxPageSize);

                    this.AggregateGroupings(sourcePage.Documents);
                }

                // Stage 2:
                // Emit the results from the grouping table page by page
                IReadOnlyList<CosmosElement> results = this.groupingTable.Drain(maxPageSize);

                QueryPage queryPage = new QueryPage(
                    documents: results,
                    requestCharge: requestCharge,
                    activityId: null,
                    responseLengthInBytes: responseLengthInBytes,
                    cosmosQueryExecutionInfo: null,
                    disallowContinuationTokenMessage: ClientGroupByQueryPipelineStage.ContinuationTokenNotSupportedWithGroupBy,
                    state: null);

                return TryCatch<QueryPage>.FromResult(queryPage);
            }
        }
    }
}
