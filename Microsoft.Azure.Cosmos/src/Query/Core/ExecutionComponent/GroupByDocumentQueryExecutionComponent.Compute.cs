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
        private static readonly IReadOnlyList<CosmosElement> EmptyResults = new List<CosmosElement>().AsReadOnly();
        private static readonly IReadOnlyDictionary<string, QueryMetrics> EmptyQueryMetrics = new Dictionary<string, QueryMetrics>();
        private static readonly string DoneReadingGroupingsContinuationToken = "DONE";

        private static readonly string UseTryGetContinuationTokenInstead = "Use TryGetContinuationTokenInstead";

        private sealed class ComputeGroupByDocumentQueryExecutionComponent : GroupByDocumentQueryExecutionComponent
        {
            private ComputeGroupByDocumentQueryExecutionComponent(
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
                GroupByContinuationToken groupByContinuationToken;
                if (requestContinuation != null)
                {
                    if (!GroupByContinuationToken.TryParse(requestContinuation, out groupByContinuationToken))
                    {
                        throw cosmosQueryClient.CreateBadRequestException(
                            $"Invalid {nameof(GroupByContinuationToken)}: '{requestContinuation}'");
                    }
                }
                else
                {
                    groupByContinuationToken = default;
                }

                IDocumentQueryExecutionComponent source;
                if (groupByContinuationToken.SourceContinuationToken == GroupByDocumentQueryExecutionComponent.DoneReadingGroupingsContinuationToken)
                {
                    source = DoneDocumentQueryExecutionComponent.Value;
                }
                else
                {
                    source = await createSourceCallback(groupByContinuationToken.SourceContinuationToken);
                }

                return new ComputeGroupByDocumentQueryExecutionComponent(
                    source,
                    cosmosQueryClient,
                    groupByAliasToAggregateType,
                    orderedAliases,
                    groupByContinuationToken.GroupingTableContinuationToken,
                    hasSelectValue,
                    groupByContinuationToken.NumPagesDrainedFromGroupingTable);
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
                        disallowContinuationTokenMessage: UseTryGetContinuationTokenInstead,
                        activityId: sourceResponse.ActivityId,
                        requestCharge: sourceResponse.RequestCharge,
                        diagnostics: sourceResponse.Diagnostics,
                        responseLengthBytes: sourceResponse.ResponseLengthBytes);

                    this.isDone = false;
                }
                else
                {
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

                    response = QueryResponseCore.CreateSuccess(
                       result: results,
                       continuationToken: null,
                       disallowContinuationTokenMessage: UseTryGetContinuationTokenInstead,
                       activityId: null,
                       requestCharge: 0,
                       diagnostics: QueryResponseCore.EmptyDiagnostics,
                       responseLengthBytes: 0);
                }

                return response;
            }

            public override bool TryGetContinuationToken(out string continuationToken)
            {
                if (this.IsDone)
                {
                    continuationToken = null;
                    return true;
                }

                if (!this.Source.TryGetContinuationToken(out string sourceContinuationToken))
                {
                    continuationToken = default;
                    return false;
                }
                if (this.Source.IsDone)
                {
                    continuationToken = new GroupByContinuationToken(
                        this.GetGroupingTableContinuationToken(),
                        GroupByDocumentQueryExecutionComponent.DoneReadingGroupingsContinuationToken,
                        this.numPagesDrainedFromGroupingTable).ToString();
                }
                else
                {
                    // Still need to drain the source.
                    continuationToken = new GroupByContinuationToken(
                        this.GetGroupingTableContinuationToken(),
                        sourceContinuationToken,
                        numPagesDrainedFromGroupingTable: 0).ToString();
                }

                return true;
            }

            private string GetGroupingTableContinuationToken()
            {
                IJsonWriter jsonWriter = JsonWriter.Create(JsonSerializationFormat.Text);
                jsonWriter.WriteObjectStart();
                foreach (KeyValuePair<UInt192, SingleGroupAggregator> kvp in this.groupingTable)
                {
                    jsonWriter.WriteFieldName(kvp.Key.ToString());
                    jsonWriter.WriteStringValue(kvp.Value.GetContinuationToken());
                }
                jsonWriter.WriteObjectEnd();

                string result = Utf8StringHelpers.ToString(jsonWriter.GetResult());
                return result;
            }

            private readonly struct GroupByContinuationToken
            {
                public GroupByContinuationToken(
                    string groupingTableContinuationToken,
                    string sourceContinuationToken,
                    int numPagesDrainedFromGroupingTable)
                {
                    this.GroupingTableContinuationToken = groupingTableContinuationToken;
                    this.SourceContinuationToken = sourceContinuationToken;
                    this.NumPagesDrainedFromGroupingTable = numPagesDrainedFromGroupingTable;
                }

                public string GroupingTableContinuationToken { get; }

                public string SourceContinuationToken { get; }

                public int NumPagesDrainedFromGroupingTable { get; }

                public override string ToString()
                {
                    return CosmosObject.Create(new Dictionary<string, CosmosElement>()
                    {
                        { nameof(this.GroupingTableContinuationToken), CosmosString.Create(this.GroupingTableContinuationToken) },
                        { nameof(this.SourceContinuationToken), CosmosString.Create(this.SourceContinuationToken) },
                        { nameof(this.NumPagesDrainedFromGroupingTable), CosmosNumber64.Create(this.NumPagesDrainedFromGroupingTable) }
                    }).ToString();
                }

                public static bool TryParse(string value, out GroupByContinuationToken groupByContinuationToken)
                {
                    if (!CosmosElement.TryParse<CosmosObject>(value, out CosmosObject groupByContinuationTokenObject))
                    {
                        groupByContinuationToken = default;
                        return false;
                    }

                    if (!groupByContinuationTokenObject.TryGetValue(
                        nameof(GroupByContinuationToken.GroupingTableContinuationToken),
                        out CosmosString groupingTableContinuationToken))
                    {
                        groupByContinuationToken = default;
                        return false;
                    }

                    if (!groupByContinuationTokenObject.TryGetValue(
                        nameof(GroupByContinuationToken.SourceContinuationToken),
                        out CosmosString sourceContinuationToken))
                    {
                        groupByContinuationToken = default;
                        return false;
                    }

                    if (!groupByContinuationTokenObject.TryGetValue(
                        nameof(GroupByContinuationToken.NumPagesDrainedFromGroupingTable),
                        out CosmosNumber64 numPagesDrainedFromGroupingTable))
                    {
                        groupByContinuationToken = default;
                        return false;
                    }

                    groupByContinuationToken = new GroupByContinuationToken(
                        groupingTableContinuationToken.Value,
                        sourceContinuationToken.Value,
                        (int)numPagesDrainedFromGroupingTable.AsInteger().Value);
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
                    throw new NotImplementedException();
                }

                public Task<QueryResponseCore> DrainAsync(int maxElements, CancellationToken token)
                {
                    throw new NotImplementedException();
                }

                public IReadOnlyDictionary<string, QueryMetrics> GetQueryMetrics()
                {
                    throw new NotImplementedException();
                }

                public void Stop()
                {
                    throw new NotImplementedException();
                }

                public bool TryGetContinuationToken(out string state)
                {
                    state = null;
                    return true;
                }
            }
        }
    }
}