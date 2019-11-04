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
    using UInt128 = Documents.UInt128;

    internal abstract partial class GroupByDocumentQueryExecutionComponent : DocumentQueryExecutionComponentBase
    {
        private sealed class ClientGroupByDocumentQueryExecutionComponent : GroupByDocumentQueryExecutionComponent
        {
            public const string ContinuationTokenNotSupportedWithGroupBy = "Continuation token is not supported for queries with GROUP BY. Do not use FeedResponse.ResponseContinuation or remove the GROUP BY from the query.";

            private ClientGroupByDocumentQueryExecutionComponent(
                IDocumentQueryExecutionComponent source,
                GroupingTable groupingTable,
                int numPagesDrainedFromGroupingTable)
                : base(
                      source,
                      groupingTable,
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
                GroupingTable groupingTable = GroupingTable.CreateFromContinuationToken(
                    cosmosQueryClient,
                    groupByAliasToAggregateType,
                    orderedAliases,
                    hasSelectValue,
                    groupingTableContinuationToken: null);
                return new ClientGroupByDocumentQueryExecutionComponent(
                    source,
                    groupingTable,
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