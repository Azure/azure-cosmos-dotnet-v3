// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.GroupBy
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Aggregate;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;

    internal abstract partial class GroupByQueryPipelineStage : QueryPipelineStageBase
    {
        private sealed class ClientGroupByQueryPipelineStage : GroupByQueryPipelineStage
        {
            public const string ContinuationTokenNotSupportedWithGroupBy = "Continuation token is not supported for queries with GROUP BY. Do not use FeedResponse.ResponseContinuation or remove the GROUP BY from the query.";
            private ClientGroupByQueryPipelineStage(
                IQueryPipelineStage source,
                CancellationToken cancellationToken,
                GroupingTable groupingTable,
                int pageSize)
                : base(source, cancellationToken, groupingTable, pageSize)
            {
            }

            public static TryCatch<IQueryPipelineStage> MonadicCreate(
                CosmosElement requestContinuation,
                CancellationToken cancellationToken,
                MonadicCreatePipelineStage monadicCreatePipelineStage,
                IReadOnlyDictionary<string, AggregateOperator?> groupByAliasToAggregateType,
                IReadOnlyList<string> orderedAliases,
                bool hasSelectValue,
                int pageSize)
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
                    tryCreateGroupingTable.Result,
                    pageSize);

                return TryCatch<IQueryPipelineStage>.FromResult(stage);
            }

            public override async ValueTask<bool> MoveNextAsync(ITrace trace)
            {
                this.cancellationToken.ThrowIfCancellationRequested();

                if (trace == null)
                {
                    throw new ArgumentNullException(nameof(trace));
                }

                if (this.returnedLastPage)
                {
                    this.Current = default;
                    return false;
                }

                // Draining GROUP BY is broken down into two stages:

                double requestCharge = 0.0;
                long responseLengthInBytes = 0;
                ImmutableDictionary<string, string> addtionalHeaders = null;

                while (await this.inputStage.MoveNextAsync(trace))
                {
                    this.cancellationToken.ThrowIfCancellationRequested();

                    // Stage 1: 
                    // Drain the groupings fully from all continuation and all partitions
                    TryCatch<QueryPage> tryGetSourcePage = this.inputStage.Current;
                    if (tryGetSourcePage.Failed)
                    {
                        this.Current = tryGetSourcePage;
                        return true;
                    }

                    QueryPage sourcePage = tryGetSourcePage.Result;

                    requestCharge += sourcePage.RequestCharge;
                    responseLengthInBytes += sourcePage.ResponseLengthInBytes;
                    addtionalHeaders = sourcePage.AdditionalHeaders;
                    this.AggregateGroupings(sourcePage.Documents);
                }

                // Stage 2:
                // Emit the results from the grouping table page by page
                IReadOnlyList<CosmosElement> results = this.groupingTable.Drain(this.pageSize);
                if (this.groupingTable.Count == 0)
                {
                    this.returnedLastPage = true;
                }

                QueryPage queryPage = new QueryPage(
                    documents: results,
                    requestCharge: requestCharge,
                    activityId: default,
                    responseLengthInBytes: responseLengthInBytes,
                    cosmosQueryExecutionInfo: default,
                    disallowContinuationTokenMessage: ClientGroupByQueryPipelineStage.ContinuationTokenNotSupportedWithGroupBy,
                    additionalHeaders: addtionalHeaders,
                    state: default);

                this.Current = TryCatch<QueryPage>.FromResult(queryPage);
                return true;
            }
        }
    }
}
