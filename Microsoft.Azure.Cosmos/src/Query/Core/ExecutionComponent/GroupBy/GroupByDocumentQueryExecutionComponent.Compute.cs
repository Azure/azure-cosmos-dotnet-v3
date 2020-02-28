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
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent.Aggregate;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;

    internal abstract partial class GroupByDocumentQueryExecutionComponent : DocumentQueryExecutionComponentBase
    {
        private sealed class ComputeGroupByDocumentQueryExecutionComponent : GroupByDocumentQueryExecutionComponent
        {
            private const string SourceTokenName = "SourceToken";
            private const string GroupingTableContinuationTokenName = "GroupingTableContinuationToken";
            private const string DoneReadingGroupingsContinuationToken = "DONE";

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
                        disallowContinuationTokenMessage: DocumentQueryExecutionComponentBase.UseSerializeStateInstead,
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
                       disallowContinuationTokenMessage: DocumentQueryExecutionComponentBase.UseSerializeStateInstead,
                       activityId: null,
                       requestCharge: 0,
                       diagnostics: QueryResponseCore.EmptyDiagnostics,
                       responseLengthBytes: 0);
                }

                return response;
            }

            public override void SerializeState(IJsonWriter jsonWriter)
            {
                if (jsonWriter == null)
                {
                    throw new ArgumentNullException(nameof(jsonWriter));
                }

                if (!this.IsDone)
                {
                    jsonWriter.WriteObjectStart();

                    jsonWriter.WriteFieldName(GroupingTableContinuationTokenName);
                    this.groupingTable.SerializeState(jsonWriter);
                    jsonWriter.WriteFieldName(SourceTokenName);
                    if (this.Source.IsDone)
                    {
                        jsonWriter.WriteStringValue(DoneReadingGroupingsContinuationToken);
                    }
                    else
                    {
                        this.Source.SerializeState(jsonWriter);
                    }

                    jsonWriter.WriteObjectEnd();
                }
            }

            private readonly struct GroupByContinuationToken
            {
                public GroupByContinuationToken(
                    CosmosElement groupingTableContinuationToken,
                    CosmosElement sourceContinuationToken)
                {
                    this.GroupingTableContinuationToken = groupingTableContinuationToken;
                    this.SourceContinuationToken = sourceContinuationToken;
                }

                public CosmosElement GroupingTableContinuationToken { get; }

                public CosmosElement SourceContinuationToken { get; }

                public static bool TryParse(CosmosElement value, out GroupByContinuationToken groupByContinuationToken)
                {
                    if (!(value is CosmosObject groupByContinuationTokenObject))
                    {
                        groupByContinuationToken = default;
                        return false;
                    }

                    if (!groupByContinuationTokenObject.TryGetValue(
                        ComputeGroupByDocumentQueryExecutionComponent.GroupingTableContinuationTokenName,
                        out CosmosElement groupingTableContinuationToken))
                    {
                        groupByContinuationToken = default;
                        return false;
                    }

                    if (!groupByContinuationTokenObject.TryGetValue(
                        ComputeGroupByDocumentQueryExecutionComponent.SourceTokenName,
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

                public IReadOnlyDictionary<string, QueryMetrics> GetQueryMetrics()
                {
                    throw new NotImplementedException();
                }

                public void SerializeState(IJsonWriter jsonWriter)
                {
                    if (jsonWriter == null)
                    {
                        throw new ArgumentNullException(nameof(jsonWriter));
                    }

                    jsonWriter.WriteNullValue();
                }

                public void Stop()
                {
                }
            }
        }
    }
}