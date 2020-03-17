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
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent.Aggregate;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;

    internal abstract partial class GroupByDocumentQueryExecutionComponent : DocumentQueryExecutionComponentBase
    {
        private sealed class ComputeGroupByDocumentQueryExecutionComponent : GroupByDocumentQueryExecutionComponent
        {
            private const string DoneReadingGroupingsContinuationToken = "DONE";
            private static readonly CosmosElement DoneCosmosElementToken = CosmosString.Create(DoneReadingGroupingsContinuationToken);

            private static readonly IReadOnlyList<CosmosElement> EmptyResults = new List<CosmosElement>().AsReadOnly();
            private static readonly IReadOnlyDictionary<string, QueryMetrics> EmptyQueryMetrics = new Dictionary<string, QueryMetrics>();

            private ComputeGroupByDocumentQueryExecutionComponent(
                IDocumentQueryExecutionComponent source,
                GroupingTable groupingTable)
                : base(
                      source,
                      groupingTable)
            {
            }

            public static async Task<TryCatch<IDocumentQueryExecutionComponent>> TryCreateAsync(
                CosmosElement requestContinuation,
                Func<CosmosElement, Task<TryCatch<IDocumentQueryExecutionComponent>>> tryCreateSourceAsync,
                IReadOnlyDictionary<string, AggregateOperator?> groupByAliasToAggregateType,
                IReadOnlyList<string> orderedAliases,
                bool hasSelectValue)
            {
                GroupByContinuationToken groupByContinuationToken;
                if (requestContinuation != null)
                {
                    if (!GroupByContinuationToken.TryParse(requestContinuation, out groupByContinuationToken))
                    {
                        return TryCatch<IDocumentQueryExecutionComponent>.FromException(
                            new MalformedContinuationTokenException($"Invalid {nameof(GroupByContinuationToken)}: '{requestContinuation}'"));
                    }
                }
                else
                {
                    groupByContinuationToken = new GroupByContinuationToken(
                        groupingTableContinuationToken: null,
                        sourceContinuationToken: null);
                }

                TryCatch<IDocumentQueryExecutionComponent> tryCreateSource;
                if ((groupByContinuationToken.SourceContinuationToken is CosmosString sourceContinuationToken)
                    && (sourceContinuationToken.Value == ComputeGroupByDocumentQueryExecutionComponent.DoneReadingGroupingsContinuationToken))
                {
                    tryCreateSource = TryCatch<IDocumentQueryExecutionComponent>.FromResult(DoneDocumentQueryExecutionComponent.Value);
                }
                else
                {
                    tryCreateSource = await tryCreateSourceAsync(groupByContinuationToken.SourceContinuationToken);
                }

                if (!tryCreateSource.Succeeded)
                {
                    return TryCatch<IDocumentQueryExecutionComponent>.FromException(tryCreateSource.Exception);
                }

                TryCatch<GroupingTable> tryCreateGroupingTable = GroupingTable.TryCreateFromContinuationToken(
                    groupByAliasToAggregateType,
                    orderedAliases,
                    hasSelectValue,
                    groupByContinuationToken.GroupingTableContinuationToken);

                if (!tryCreateGroupingTable.Succeeded)
                {
                    return TryCatch<IDocumentQueryExecutionComponent>.FromException(tryCreateGroupingTable.Exception);
                }

                return TryCatch<IDocumentQueryExecutionComponent>.FromResult(
                    new ComputeGroupByDocumentQueryExecutionComponent(
                        tryCreateSource.Result,
                        tryCreateGroupingTable.Result));
            }

            public override async Task<QueryResponseCore> DrainAsync(
                int maxElements,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Draining GROUP BY is broken down into two stages:
                QueryResponseCore response;
                if (!this.Source.IsDone)
                {
                    // Stage 1: 
                    // Drain the groupings fully from all continuation and all partitions
                    QueryResponseCore sourceResponse = await base.DrainAsync(int.MaxValue, cancellationToken);
                    if (!sourceResponse.IsSuccess)
                    {
                        return sourceResponse;
                    }

                    this.AggregateGroupings(sourceResponse.CosmosElements);

                    // We need to give empty pages until the results are fully drained.
                    response = QueryResponseCore.CreateSuccess(
                        result: EmptyResults,
                        continuationToken: null,
                        disallowContinuationTokenMessage: DocumentQueryExecutionComponentBase.UseCosmosElementContinuationTokenInstead,
                        activityId: sourceResponse.ActivityId,
                        requestCharge: sourceResponse.RequestCharge,
                        diagnostics: sourceResponse.Diagnostics,
                        responseLengthBytes: sourceResponse.ResponseLengthBytes);
                }
                else
                {
                    // Stage 2:
                    // Emit the results from the grouping table page by page
                    IReadOnlyList<CosmosElement> results = this.groupingTable.Drain(maxElements);

                    response = QueryResponseCore.CreateSuccess(
                       result: results,
                       continuationToken: null,
                       disallowContinuationTokenMessage: DocumentQueryExecutionComponentBase.UseCosmosElementContinuationTokenInstead,
                       activityId: null,
                       requestCharge: 0,
                       diagnostics: QueryResponseCore.EmptyDiagnostics,
                       responseLengthBytes: 0);
                }

                return response;
            }

            public override CosmosElement GetCosmosElementContinuationToken()
            {
                if (this.IsDone)
                {
                    return default;
                }

                CosmosElement sourceContinuationToken;
                if (this.Source.IsDone)
                {
                    sourceContinuationToken = DoneCosmosElementToken;
                }
                else
                {
                    sourceContinuationToken = this.Source.GetCosmosElementContinuationToken();
                }

                GroupByContinuationToken groupByContinuationToken = new GroupByContinuationToken(
                    groupingTableContinuationToken: this.groupingTable.GetCosmosElementContinuationToken(),
                    sourceContinuationToken: sourceContinuationToken);

                return GroupByContinuationToken.ToCosmosElement(groupByContinuationToken);
            }

            public override bool TryGetFeedToken(
                string containerResourceId,
                out FeedToken feedToken)
            {
                if (this.IsDone)
                {
                    feedToken = null;
                    return true;
                }

                if (!this.Source.TryGetFeedToken(containerResourceId, out feedToken))
                {
                    feedToken = null;
                    return false;
                }

                CosmosElement sourceContinuationToken;
                if (this.Source.IsDone)
                {
                    sourceContinuationToken = DoneCosmosElementToken;
                }
                else
                {
                    sourceContinuationToken = this.Source.GetCosmosElementContinuationToken();
                }

                GroupByContinuationToken groupByContinuationToken = new GroupByContinuationToken(
                    groupingTableContinuationToken: this.groupingTable.GetCosmosElementContinuationToken(),
                    sourceContinuationToken: sourceContinuationToken);

                if (feedToken is FeedTokenEPKRange feedTokenInternal)
                {
                    feedToken = FeedTokenEPKRange.Copy(
                            feedTokenInternal,
                            GroupByContinuationToken.ToCosmosElement(groupByContinuationToken).ToString());
                }
                else if (this.Source.IsDone)
                {
                    // If source is done, feedToken is null, there are no more ranges but GroupBy requires one more iteration
                    feedToken = new FeedTokenEPKRange(
                            containerResourceId,
                            new Documents.Routing.Range<string>(
                                Documents.Routing.PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                                Documents.Routing.PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                                isMinInclusive: true,
                                isMaxInclusive: false),
                            GroupByContinuationToken.ToCosmosElement(groupByContinuationToken).ToString());
                }

                return true;
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

            private sealed class DoneDocumentQueryExecutionComponent : IDocumentQueryExecutionComponent
            {
                public static readonly DoneDocumentQueryExecutionComponent Value = new DoneDocumentQueryExecutionComponent();

                private DoneDocumentQueryExecutionComponent()
                {
                }

                public bool IsDone => true;

                public void Dispose()
                {
                    // Do Nothing
                }

                public Task<QueryResponseCore> DrainAsync(int maxElements, CancellationToken token)
                {
                    token.ThrowIfCancellationRequested();
                    throw new NotImplementedException();
                }

                public CosmosElement GetCosmosElementContinuationToken()
                {
                    throw new NotImplementedException();
                }

                public IReadOnlyDictionary<string, QueryMetrics> GetQueryMetrics()
                {
                    throw new NotImplementedException();
                }

                public void Stop()
                {
                }

                public bool TryGetFeedToken(
                    string containerResourceId,
                    out FeedToken feedToken)
                {
                    feedToken = null;
                    return true;
                }
            }
        }
    }
}