//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.ExecutionComponent
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;

    /// <summary>
    /// Query execution component that groups groupings across continuations and pages.
    /// The general idea is a query gets rewritten from this:
    /// 
    /// SELECT c.team, c.name, COUNT(1) AS count
    /// FROM c
    /// GROUP BY c.team, c.name
    /// 
    /// To this:
    /// 
    /// SELECT 
    ///     [{"item": c.team}, {"item": c.name}] AS groupByItems, 
    ///     {"team": c.team, "name": c.name, "count": {"item": COUNT(1)}} AS payload
    /// FROM c
    /// GROUP BY c.team, c.name
    /// 
    /// With the following dictionary:
    /// 
    /// {
    ///     "team": null,
    ///     "name": null,
    ///     "count" COUNT
    /// }
    /// 
    /// So we know how to aggregate each column. 
    /// At the end the columns are stitched together to make the grouped document.
    /// </summary>
    internal sealed class GroupByDocumentQueryExecutionComponent : DocumentQueryExecutionComponentBase
    {
        private static readonly List<CosmosElement> EmptyResults = new List<CosmosElement>();
        private static readonly Dictionary<string, QueryMetrics> EmptyQueryMetrics = new Dictionary<string, QueryMetrics>();
        private static readonly AggregateOperator[] EmptyAggregateOperators = new AggregateOperator[] { };
        private static readonly string DoneReadingGroupingsContinuationToken = "DONE";

        private readonly CosmosQueryClient cosmosQueryClient;
        private readonly IReadOnlyDictionary<string, AggregateOperator?> groupByAliasToAggregateType;
        private readonly Dictionary<UInt192, SingleGroupAggregator> groupingTable;
        private readonly DistinctMap distinctMap;
        private readonly bool hasSelectValue;

        private int numPagesDrainedFromGroupingTable;
        private bool isDone;

        private GroupByDocumentQueryExecutionComponent(
            IDocumentQueryExecutionComponent source,
            CosmosQueryClient cosmosQueryClient,
            IReadOnlyDictionary<string, AggregateOperator?> groupByAliasToAggregateType,
            string groupingTableContinuationToken,
            bool hasSelectValue,
            int numPagesDrainedFromGroupingTable)
            : base(source)
        {
            if (cosmosQueryClient == null)
            {
                throw new ArgumentNullException(nameof(cosmosQueryClient));
            }

            if (groupByAliasToAggregateType == null)
            {
                throw new ArgumentNullException(nameof(groupByAliasToAggregateType));
            }

            this.cosmosQueryClient = cosmosQueryClient;
            this.groupingTable = new Dictionary<UInt192, SingleGroupAggregator>();

            // Using an ordered distinct map to get hashes.
            this.distinctMap = DistinctMap.Create(DistinctQueryType.Ordered, null);
            this.groupByAliasToAggregateType = groupByAliasToAggregateType;
            this.hasSelectValue = hasSelectValue;
            this.numPagesDrainedFromGroupingTable = numPagesDrainedFromGroupingTable;

            if (groupingTableContinuationToken != null)
            {
                if (!CosmosElement.TryParse<CosmosObject>(
                    groupingTableContinuationToken,
                    out CosmosObject parsedGroupingTableContinuations))
                {
                    throw cosmosQueryClient.CreateBadRequestException($"Invalid GroupingTableContinuationToken");
                }

                foreach (KeyValuePair<string, CosmosElement> kvp in parsedGroupingTableContinuations)
                {
                    string key = kvp.Key;
                    CosmosElement value = kvp.Value;

                    UInt192 groupByKey = UInt192.Parse(key);

                    if (!(value is CosmosString singleGroupAggregatorContinuationToken))
                    {
                        throw cosmosQueryClient.CreateBadRequestException($"Invalid GroupingTableContinuationToken");
                    }
                    SingleGroupAggregator singleGroupAggregator = SingleGroupAggregator.Create(
                        this.cosmosQueryClient,
                        EmptyAggregateOperators,
                        this.groupByAliasToAggregateType,
                        this.hasSelectValue,
                        singleGroupAggregatorContinuationToken.Value);

                    this.groupingTable[groupByKey] = singleGroupAggregator;
                }
            }
        }

        public override bool IsDone => this.isDone;

        public static async Task<IDocumentQueryExecutionComponent> CreateAsync(
            CosmosQueryClient cosmosQueryClient,
            string requestContinuation,
            Func<string, Task<IDocumentQueryExecutionComponent>> createSourceCallback,
            IReadOnlyDictionary<string, AggregateOperator?> groupByAliasToAggregateType,
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
                groupByContinuationToken = default(GroupByContinuationToken);
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

            return new GroupByDocumentQueryExecutionComponent(
                source,
                cosmosQueryClient,
                groupByAliasToAggregateType,
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

                foreach (CosmosElement result in sourceResponse.CosmosElements)
                {
                    // Aggregate the values for all groupings across all continuations.
                    RewrittenGroupByProjection groupByItem = new RewrittenGroupByProjection(result);
                    this.distinctMap.Add(groupByItem.GroupByItems, out UInt192? groupByKeysHash);
                    if (!groupByKeysHash.HasValue)
                    {
                        throw new InvalidOperationException("hash invariant was broken");
                    }

                    if (!this.groupingTable.TryGetValue(groupByKeysHash.Value, out SingleGroupAggregator singleGroupAggregator))
                    {
                        singleGroupAggregator = SingleGroupAggregator.Create(
                            this.cosmosQueryClient,
                            EmptyAggregateOperators,
                            this.groupByAliasToAggregateType,
                            this.hasSelectValue,
                            continuationToken: null);
                        this.groupingTable[groupByKeysHash.Value] = singleGroupAggregator;
                    }

                    CosmosElement payload = groupByItem.Payload;
                    singleGroupAggregator.AddValues(payload);
                }

                string updatedContinuationToken;
                if (this.Source.IsDone)
                {
                    updatedContinuationToken = new GroupByContinuationToken(
                        this.GetGroupingTableContinuationToken(),
                        GroupByDocumentQueryExecutionComponent.DoneReadingGroupingsContinuationToken,
                        numPagesDrainedFromGroupingTable: 0).ToString();
                }
                else
                {
                    updatedContinuationToken = new GroupByContinuationToken(
                        this.GetGroupingTableContinuationToken(),
                        sourceResponse.ContinuationToken,
                        numPagesDrainedFromGroupingTable: 0).ToString();
                }

                // We need to give empty pages until the results are fully drained.
                response = QueryResponseCore.CreateSuccess(
                    result: EmptyResults,
                    continuationToken: updatedContinuationToken,
                    disallowContinuationTokenMessage: null,
                    activityId: sourceResponse.ActivityId,
                    requestCharge: sourceResponse.RequestCharge,
                    queryMetricsText: sourceResponse.QueryMetricsText,
                    queryMetrics: sourceResponse.QueryMetrics,
                    requestStatistics: sourceResponse.RequestStatistics,
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
                if (this.numPagesDrainedFromGroupingTable * maxElements >= this.groupingTable.Count)
                {
                    this.isDone = true;
                }

                string continuationToken;
                if (this.isDone)
                {
                    continuationToken = null;
                }
                else
                {
                    continuationToken = new GroupByContinuationToken(
                        this.GetGroupingTableContinuationToken(),
                        sourceContinuationToken: GroupByDocumentQueryExecutionComponent.DoneReadingGroupingsContinuationToken,
                        numPagesDrainedFromGroupingTable: this.numPagesDrainedFromGroupingTable).ToString();
                }

                response = QueryResponseCore.CreateSuccess(
                   result: results,
                   continuationToken: continuationToken,
                   disallowContinuationTokenMessage: null,
                   activityId: null,
                   requestCharge: 0,
                   queryMetricsText: null,
                   queryMetrics: EmptyQueryMetrics,
                   requestStatistics: null,
                   responseLengthBytes: 0);
            }

            return response;
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

            string result = Encoding.UTF8.GetString(jsonWriter.GetResult());
            return result;
        }

        private struct GroupByContinuationToken
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
                    groupByContinuationToken = default(GroupByContinuationToken);
                    return false;
                }

                if (!groupByContinuationTokenObject.TryGetValue<CosmosString>(
                    nameof(GroupByContinuationToken.GroupingTableContinuationToken),
                    out CosmosString groupingTableContinuationToken))
                {
                    groupByContinuationToken = default(GroupByContinuationToken);
                    return false;
                }

                if (!groupByContinuationTokenObject.TryGetValue<CosmosString>(
                    nameof(GroupByContinuationToken.SourceContinuationToken),
                    out CosmosString sourceContinuationToken))
                {
                    groupByContinuationToken = default(GroupByContinuationToken);
                    return false;
                }

                if (!groupByContinuationTokenObject.TryGetValue<CosmosNumber64>(
                    nameof(GroupByContinuationToken.NumPagesDrainedFromGroupingTable),
                    out CosmosNumber64 numPagesDrainedFromGroupingTable))
                {
                    groupByContinuationToken = default(GroupByContinuationToken);
                    return false;
                }

                groupByContinuationToken = new GroupByContinuationToken(
                    groupingTableContinuationToken.Value,
                    sourceContinuationToken.Value,
                    (int)numPagesDrainedFromGroupingTable.AsInteger().Value);
                return true;
            }
        }

        /// <summary>
        /// When a group by query gets rewritten the projection looks like:
        /// 
        /// SELECT 
        ///     [{"item": c.age}, {"item": c.name}] AS groupByItems, 
        ///     {"age": c.age, "name": c.name} AS payload
        /// 
        /// This struct just lets us easily access the "groupByItems" and "payload" property.
        /// </summary>
        private struct RewrittenGroupByProjection
        {
            private const string GroupByItemsPropertyName = "groupByItems";
            private const string PayloadPropertyName = "payload";

            private readonly CosmosObject cosmosObject;

            public RewrittenGroupByProjection(CosmosElement cosmosElement)
            {
                if (cosmosElement == null)
                {
                    throw new ArgumentNullException(nameof(cosmosElement));
                }

                if (!(cosmosElement is CosmosObject cosmosObject))
                {
                    throw new ArgumentException($"{nameof(cosmosElement)} must not be an object.");
                }

                this.cosmosObject = cosmosObject;
            }

            public CosmosArray GroupByItems
            {
                get
                {
                    if (!this.cosmosObject.TryGetValue(GroupByItemsPropertyName, out CosmosElement cosmosElement))
                    {
                        throw new InvalidOperationException($"Underlying object does not have an 'groupByItems' field.");
                    }

                    if (!(cosmosElement is CosmosArray cosmosArray))
                    {
                        throw new ArgumentException($"{nameof(RewrittenGroupByProjection)}['groupByItems'] was not an array.");
                    }

                    return cosmosArray;
                }
            }

            public CosmosElement Payload
            {
                get
                {
                    if (!this.cosmosObject.TryGetValue(PayloadPropertyName, out CosmosElement cosmosElement))
                    {
                        throw new InvalidOperationException($"Underlying object does not have an 'payload' field.");
                    }

                    return cosmosElement;
                }
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
        }
    }
}