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
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Aggregate;

    internal abstract partial class GroupByQueryPipelineStage : QueryPipelineStageBase
    {
        private sealed class ComputeGroupByQueryPipelineStage : GroupByQueryPipelineStage
        {
            private const string DoneReadingGroupingsContinuationToken = "DONE";
            private static readonly CosmosElement DoneCosmosElementToken = CosmosString.Create(DoneReadingGroupingsContinuationToken);

            private static readonly IReadOnlyList<CosmosElement> EmptyResults = new List<CosmosElement>().AsReadOnly();
            private static readonly IReadOnlyDictionary<string, QueryMetrics> EmptyQueryMetrics = new Dictionary<string, QueryMetrics>();

            private ComputeGroupByQueryPipelineStage(
                IQueryPipelineStage source,
                GroupingTable groupingTable)
                : base(
                      source,
                      groupingTable)
            {
            }

            public static async Task<TryCatch<IQueryPipelineStage>> TryCreateAsync(
                CosmosElement requestContinuation,
                Func<CosmosElement, Task<TryCatch<IQueryPipelineStage>>> tryCreateSourceAsync,
                IReadOnlyDictionary<string, AggregateOperator?> groupByAliasToAggregateType,
                IReadOnlyList<string> orderedAliases,
                bool hasSelectValue)
            {
                GroupByContinuationToken groupByContinuationToken;
                if (requestContinuation != null)
                {
                    if (!GroupByContinuationToken.TryParse(requestContinuation, out groupByContinuationToken))
                    {
                        return TryCatch<IQueryPipelineStage>.FromException(
                            new MalformedContinuationTokenException(
                                $"Invalid {nameof(GroupByContinuationToken)}: '{requestContinuation}'"));
                    }
                }
                else
                {
                    groupByContinuationToken = new GroupByContinuationToken(
                        groupingTableContinuationToken: null,
                        sourceContinuationToken: null);
                }

                TryCatch<IQueryPipelineStage> tryCreateSource;
                if ((groupByContinuationToken.SourceContinuationToken is CosmosString sourceContinuationToken)
                    && (sourceContinuationToken.Value == ComputeGroupByQueryPipelineStage.DoneReadingGroupingsContinuationToken))
                {
                    tryCreateSource = TryCatch<IQueryPipelineStage>.FromResult(FinishedQueryPipelineStage.Value);
                }
                else
                {
                    tryCreateSource = await tryCreateSourceAsync(groupByContinuationToken.SourceContinuationToken);
                }

                if (!tryCreateSource.Succeeded)
                {
                    return TryCatch<IQueryPipelineStage>.FromException(tryCreateSource.Exception);
                }

                TryCatch<GroupingTable> tryCreateGroupingTable = GroupingTable.TryCreateFromContinuationToken(
                    groupByAliasToAggregateType,
                    orderedAliases,
                    hasSelectValue,
                    groupByContinuationToken.GroupingTableContinuationToken);

                if (!tryCreateGroupingTable.Succeeded)
                {
                    return TryCatch<IQueryPipelineStage>.FromException(tryCreateGroupingTable.Exception);
                }

                return TryCatch<IQueryPipelineStage>.FromResult(
                    new ComputeGroupByQueryPipelineStage(
                        tryCreateSource.Result,
                        tryCreateGroupingTable.Result));
            }

            protected override async Task<TryCatch<QueryPage>> GetNextPageAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Draining GROUP BY is broken down into two stages:
                QueryPage queryPage;
                if (await this.inputStage.MoveNextAsync())
                {
                    // Stage 1: 
                    // Drain the groupings fully from all continuation and all partitions
                    TryCatch<QueryPage> tryGetSourcePage = this.inputStage.Current;
                    if (tryGetSourcePage.Failed)
                    {
                        return tryGetSourcePage;
                    }

                    QueryPage sourcePage = tryGetSourcePage.Result;

                    this.AggregateGroupings(sourcePage.Documents);

                    // We need to give empty pages until the results are fully drained.
                    CosmosElement sourceContinuationToken = sourcePage.State == null ? DoneCosmosElementToken : sourcePage.State.Value;
                    GroupByContinuationToken groupByContinuationToken = new GroupByContinuationToken(
                        groupingTableContinuationToken: this.groupingTable.GetCosmosElementContinuationToken(),
                        sourceContinuationToken: sourceContinuationToken);
                    QueryState state = new QueryState(GroupByContinuationToken.ToCosmosElement(groupByContinuationToken));

                    queryPage = new QueryPage(
                        documents: EmptyResults,
                        requestCharge: sourcePage.RequestCharge,
                        activityId: sourcePage.ActivityId,
                        responseLengthInBytes: sourcePage.ResponseLengthInBytes,
                        cosmosQueryExecutionInfo: sourcePage.CosmosQueryExecutionInfo,
                        disallowContinuationTokenMessage: null,
                        state: state);
                }
                else
                {
                    // Stage 2:
                    // Emit the results from the grouping table page by page
                    IReadOnlyList<CosmosElement> results = this.groupingTable.Drain(10/*FIX THIS*/);

                    QueryState state;
                    if (this.groupingTable.IsDone)
                    {
                        state = default;
                    }
                    else
                    {
                        GroupByContinuationToken groupByContinuationToken = new GroupByContinuationToken(
                            groupingTableContinuationToken: this.groupingTable.GetCosmosElementContinuationToken(),
                            sourceContinuationToken: DoneCosmosElementToken);
                        state = new QueryState(GroupByContinuationToken.ToCosmosElement(groupByContinuationToken));
                    }

                    queryPage = new QueryPage(
                        documents: results,
                        requestCharge: default,
                        activityId: default,
                        responseLengthInBytes: default,
                        cosmosQueryExecutionInfo: default,
                        disallowContinuationTokenMessage: default,
                        state: state);
                }

                return TryCatch<QueryPage>.FromResult(queryPage);
            }

            private readonly struct GroupByContinuationToken
            {
                private static class PropertyNames
                {
                    public const string SourceToken = "SourceToken";
                    public const string GroupingTableContinuationToken = "GroupingTableContinuationToken";
                }

                public GroupByContinuationToken(
                    CosmosElement groupingTableContinuationToken,
                    CosmosElement sourceContinuationToken)
                {
                    this.GroupingTableContinuationToken = groupingTableContinuationToken;
                    this.SourceContinuationToken = sourceContinuationToken;
                }

                public CosmosElement GroupingTableContinuationToken { get; }

                public CosmosElement SourceContinuationToken { get; }

                public static CosmosElement ToCosmosElement(GroupByContinuationToken groupByContinuationToken)
                {
                    Dictionary<string, CosmosElement> dictionary = new Dictionary<string, CosmosElement>()
                    {
                        {
                            GroupByContinuationToken.PropertyNames.SourceToken,
                            groupByContinuationToken.SourceContinuationToken
                        },
                        {
                            GroupByContinuationToken.PropertyNames.GroupingTableContinuationToken,
                            groupByContinuationToken.GroupingTableContinuationToken
                        },
                    };

                    return CosmosObject.Create(dictionary);
                }

                public static bool TryParse(CosmosElement value, out GroupByContinuationToken groupByContinuationToken)
                {
                    if (!(value is CosmosObject groupByContinuationTokenObject))
                    {
                        groupByContinuationToken = default;
                        return false;
                    }

                    if (!groupByContinuationTokenObject.TryGetValue(
                        GroupByContinuationToken.PropertyNames.GroupingTableContinuationToken,
                        out CosmosElement groupingTableContinuationToken))
                    {
                        groupByContinuationToken = default;
                        return false;
                    }

                    if (!groupByContinuationTokenObject.TryGetValue(
                        GroupByContinuationToken.PropertyNames.SourceToken,
                        out CosmosElement sourceContinuationToken))
                    {
                        groupByContinuationToken = default;
                        return false;
                    }

                    groupByContinuationToken = new GroupByContinuationToken(
                        groupingTableContinuationToken: groupingTableContinuationToken,
                        sourceContinuationToken: sourceContinuationToken);
                    return true;
                }
            }
        }
    }
}
