//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent.GroupBy
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent.Aggregate;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;

    internal abstract partial class GroupByDocumentQueryExecutionComponent : DocumentQueryExecutionComponentBase
    {
        private sealed class ClientGroupByDocumentQueryExecutionComponent : GroupByDocumentQueryExecutionComponent
        {
            public const string ContinuationTokenNotSupportedWithGroupBy = "Continuation token is not supported for queries with GROUP BY. Do not use FeedResponse.ResponseContinuation or remove the GROUP BY from the query.";

            private ClientGroupByDocumentQueryExecutionComponent(
                IDocumentQueryExecutionComponent source,
                GroupingTable groupingTable)
                : base(
                      source,
                      groupingTable)
            {
            }

            public static async Task<TryCatch<IDocumentQueryExecutionComponent>> TryCreateAsync(
                CosmosElement requestContinuation,
                Func<CosmosElement, Task<TryCatch<IDocumentQueryExecutionComponent>>> tryCreateSource,
                IReadOnlyDictionary<string, AggregateOperator?> groupByAliasToAggregateType,
                IReadOnlyList<string> orderedAliases,
                bool hasSelectValue)
            {
                TryCatch<GroupingTable> tryCreateGroupingTable = GroupingTable.TryCreateFromContinuationToken(
                    groupByAliasToAggregateType,
                    orderedAliases,
                    hasSelectValue,
                    continuationToken: null);

                if (!tryCreateGroupingTable.Succeeded)
                {
                    return TryCatch<IDocumentQueryExecutionComponent>.FromException(tryCreateGroupingTable.Exception);
                }

                return (await tryCreateSource(requestContinuation)).Try<IDocumentQueryExecutionComponent>(source =>
                {
                    return new ClientGroupByDocumentQueryExecutionComponent(
                        source,
                        tryCreateGroupingTable.Result);
                });
            }

            public override async Task<QueryResponseCore> DrainAsync(
                int maxElements,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Draining GROUP BY is broken down into two stages:

                double requestCharge = 0.0;
                long responseLengthBytes = 0;
                List<QueryPageDiagnostics> queryPageDiagnostics = new List<QueryPageDiagnostics>();
                while (!this.Source.IsDone)
                {
                    // Stage 1: 
                    // Drain the groupings fully from all continuation and all partitions
                    QueryResponseCore sourceResponse = await base.DrainAsync(int.MaxValue, cancellationToken);
                    if (!sourceResponse.IsSuccess)
                    {
                        return sourceResponse;
                    }

                    requestCharge += sourceResponse.RequestCharge;
                    responseLengthBytes += sourceResponse.ResponseLengthBytes;
                    if (sourceResponse.Diagnostics != null)
                    {
                        queryPageDiagnostics.AddRange(sourceResponse.Diagnostics);
                    }

                    this.AggregateGroupings(sourceResponse.CosmosElements);
                }

                // Stage 2:
                // Emit the results from the grouping table page by page
                IReadOnlyList<CosmosElement> results = this.groupingTable.Drain(maxElements);

                QueryResponseCore response = QueryResponseCore.CreateSuccess(
                   result: results,
                   continuationToken: null,
                   disallowContinuationTokenMessage: ClientGroupByDocumentQueryExecutionComponent.ContinuationTokenNotSupportedWithGroupBy,
                   activityId: null,
                   requestCharge: requestCharge,
                   diagnostics: queryPageDiagnostics,
                   responseLengthBytes: responseLengthBytes);

                return response;
            }

            public override CosmosElement GetCosmosElementContinuationToken()
            {
                throw new NotImplementedException();
            }
        }
    }
}