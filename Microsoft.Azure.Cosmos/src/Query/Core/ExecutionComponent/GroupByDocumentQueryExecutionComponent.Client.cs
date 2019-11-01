//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;

    internal abstract partial class GroupByDocumentQueryExecutionComponent : DocumentQueryExecutionComponentBase
    {
        public const string ContinuationTokenNotSupportedWithGroupBy = "Continuation token is not supported for queries with GROUP BY. Do not use FeedResponse.ResponseContinuation or remove the GROUP BY from the query.";

        private sealed class ClientGroupByDocumentQueryExecutionComponent : GroupByDocumentQueryExecutionComponent
        {
            private ClientGroupByDocumentQueryExecutionComponent(
                IDocumentQueryExecutionComponent source,
                CosmosQueryClient cosmosQueryClient,
                IReadOnlyDictionary<string, AggregateOperator?> groupByAliasToAggregateType,
                IReadOnlyList<string> orderedAliases,
                string groupingTableContinuationToken,
                bool hasSelectValue,
                int numPagesDrainedFromGroupingTable)
                : base(
                      source,
                      cosmosQueryClient,
                      groupByAliasToAggregateType,
                      orderedAliases,
                      groupingTableContinuationToken,
                      hasSelectValue,
                      numPagesDrainedFromGroupingTable)
            {
            }

            public static async Task<IDocumentQueryExecutionComponent> CreateAsync(
                CosmosQueryClient cosmosQueryClient,
                string requestContinuation,
                Func<string, Task<IDocumentQueryExecutionComponent>> createSourceCallback,
                IReadOnlyDictionary<string, AggregateOperator?> groupByAliasToAggregateType,
                IReadOnlyList<string> orderedAliases,
                bool hasSelectValue)
            {
                IDocumentQueryExecutionComponent source = await createSourceCallback(requestContinuation);
                return new ClientGroupByDocumentQueryExecutionComponent(
                    source,
                    cosmosQueryClient,
                    groupByAliasToAggregateType,
                    orderedAliases,
                    groupingTableContinuationToken: null,
                    hasSelectValue,
                    numPagesDrainedFromGroupingTable: 0);
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

                    this.isDone = false;
                }

                // Stage 2:
                // Emit the results from the grouping table page by page
                IEnumerable<SingleGroupAggregator> groupByValuesList = this.groupingTable
                    .OrderBy(kvp => kvp.Key)
                    .Skip(this.numPagesDrainedFromGroupingTable * maxElements)
                    .Take(maxElements)
                    .Select(kvp => kvp.Value);

                List<CosmosElement> results = new List<CosmosElement>();
                foreach (SingleGroupAggregator groupByValues in groupByValuesList)
                {
                    results.Add(groupByValues.GetResult());
                }

                this.numPagesDrainedFromGroupingTable++;
                if ((this.numPagesDrainedFromGroupingTable * maxElements) >= this.groupingTable.Count)
                {
                    this.isDone = true;
                }

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

            public override bool TryGetContinuationToken(out string state)
            {
                state = default;
                return false;
            }
        }
    }
}