//-----------------------------------------------------------------------
// <copyright file="DistinctDocumentQueryExecutionComponent.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.ExecutionComponent
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Aggregation;
    using Microsoft.Azure.Documents.Collections;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

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
        public const string ContinuationTokenNotSupportedWithGroupBy = "Continuation token is not supported for queries with GROUP BY. Do not use FeedResponse.ResponseContinuation or remove the GROUP BY from the query.";
        private static readonly List<CosmosElement> EmptyResults = new List<CosmosElement>();
        private static readonly StringKeyValueCollection EmptyHeaders = new StringKeyValueCollection();
        private static readonly Dictionary<string, QueryMetrics> EmptyQueryMetrics = new Dictionary<string, QueryMetrics>();

        private readonly IReadOnlyDictionary<string, AggregateOperator?> groupByAliasToAggregateType;
        private readonly Dictionary<UInt192, GroupByValues> groupingTable;
        private readonly DistinctMap distinctMap;

        private int numPagesDrainedFromGroupingTable;
        private bool isDone;

        private GroupByDocumentQueryExecutionComponent(
            IReadOnlyDictionary<string, AggregateOperator?> groupByAliasToAggregateType,
            IDocumentQueryExecutionComponent source)
            : base(source)
        {
            if (groupByAliasToAggregateType == null)
            {
                throw new ArgumentNullException(nameof(groupByAliasToAggregateType));
            }

            this.groupingTable = new Dictionary<UInt192, GroupByValues>();

            // Using an ordered distinct map to get hashes.
            this.distinctMap = DistinctMap.Create(DistinctQueryType.Ordered, null);
            this.groupByAliasToAggregateType = groupByAliasToAggregateType;
        }

        public override bool IsDone
        {
            get
            {
                return this.isDone;
            }
        }

        public static async Task<IDocumentQueryExecutionComponent> CreateAsync(
            string requestContinuation,
            Func<string, Task<IDocumentQueryExecutionComponent>> createSourceCallback,
            IReadOnlyDictionary<string, AggregateOperator?> groupByAliasToAggregateType)
        {
            // We do not support continuation tokens for GROUP BY.
            return new GroupByDocumentQueryExecutionComponent(
                groupByAliasToAggregateType,
                await createSourceCallback(requestContinuation));
        }

        public override async Task<FeedResponse<CosmosElement>> DrainAsync(
            int maxElements,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Draining GROUP BY is broken down into two stages:
            FeedResponse<CosmosElement> response;
            if (!this.Source.IsDone)
            {
                // Stage 1: 
                // Drain the groupings fully from all continuation and all partitions
                FeedResponse<CosmosElement> results = await base.DrainAsync(int.MaxValue, cancellationToken);
                foreach (CosmosElement result in results)
                {
                    // Aggregate the values for all groupings across all continuations.
                    RewrittenGroupByProjection groupByItem = new RewrittenGroupByProjection(result);
                    this.distinctMap.Add(groupByItem.GroupByItems, out UInt192? groupByKeysHash);
                    if (!groupByKeysHash.HasValue)
                    {
                        throw new InvalidOperationException("hash invariant was broken");
                    }

                    if (!this.groupingTable.TryGetValue(groupByKeysHash.Value, out GroupByValues groupByValues))
                    {
                        groupByValues = GroupByValues.CreateFromAggregateTypeDictionary(this.groupByAliasToAggregateType);
                        this.groupingTable[groupByKeysHash.Value] = groupByValues;
                    }

                    CosmosElement payload = groupByItem.Payload;
                    groupByValues.AddValues(payload);
                }

                // We need to give empty pages until the results are fully drained.
                response = new FeedResponse<CosmosElement>(
                    EmptyResults,
                    EmptyResults.Count,
                    results.Headers,
                    results.UseETagAsContinuation,
                    results.QueryMetrics,
                    results.RequestStatistics,
                    GroupByDocumentQueryExecutionComponent.ContinuationTokenNotSupportedWithGroupBy,
                    results.ResponseLengthBytes);

                this.isDone = false;
            }
            else
            {
                // Stage 2:
                // Emit the results from the grouping table page by page
                IEnumerable<GroupByValues> groupByValuesList = this.groupingTable
                    .OrderBy(kvp => kvp.Key)
                    .Skip(this.numPagesDrainedFromGroupingTable * maxElements)
                    .Take(maxElements)
                    .Select(kvp => kvp.Value);

                List<CosmosElement> results = new List<CosmosElement>();
                foreach (GroupByValues groupByValues in groupByValuesList)
                {
                    results.Add(groupByValues.GetResult());
                }

                response = new FeedResponse<CosmosElement>(
                    results,
                    results.Count,
                    EmptyHeaders,
                    queryMetrics: EmptyQueryMetrics,
                    disallowContinuationTokenMessage: GroupByDocumentQueryExecutionComponent.ContinuationTokenNotSupportedWithGroupBy);

                this.numPagesDrainedFromGroupingTable++;
                if (this.numPagesDrainedFromGroupingTable * maxElements >= this.groupingTable.Count)
                {
                    this.isDone = true;
                }
            }

            return response;
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

        /// <summary>
        /// Represents all the values in a group by projection.
        /// </summary>
        private abstract class GroupByValues
        {
            /// <summary>
            /// Adds the payload for group by values 
            /// </summary>
            /// <param name="values"></param>
            public abstract void AddValues(CosmosElement values);

            /// <summary>
            /// Forms the final result of the grouping.
            /// </summary>
            /// <returns></returns>
            public abstract CosmosElement GetResult();

            public static GroupByValues CreateFromAggregateTypeDictionary(
                IReadOnlyDictionary<string, AggregateOperator?> groupByAliasToAggregateType)
            {
                GroupByValues groupByValues;
                if ((groupByAliasToAggregateType == null) || (groupByAliasToAggregateType.Count() == 0))
                {
                    groupByValues = SelectValueGroupByValues.Create();
                }
                else
                {
                    groupByValues = SelectListGroupByValues.Create(groupByAliasToAggregateType);
                }

                return groupByValues;
            }

            /// <summary>
            /// For SELECT VALUE queries there is only one value for each grouping.
            /// This class just helps maintain that and captures the first value across all continuations.
            /// </summary>
            private sealed class SelectValueGroupByValues : GroupByValues
            {
                private readonly GroupByValue groupByValue;

                private SelectValueGroupByValues(GroupByValue groupByValue)
                {
                    if (groupByValue == null)
                    {
                        throw new ArgumentNullException(nameof(groupByValue));
                    }

                    this.groupByValue = groupByValue;
                }

                public static SelectValueGroupByValues Create()
                {
                    GroupByValue groupByValue = GroupByValue.Create(aggregateOperator: null);
                    return new SelectValueGroupByValues(groupByValue);
                }

                public override void AddValues(CosmosElement values)
                {
                    this.groupByValue.AddValue(values);
                }

                public override CosmosElement GetResult()
                {
                    return this.groupByValue.Result;
                }

                public override string ToString()
                {
                    return this.groupByValue.ToString();
                }
            }

            /// <summary>
            /// For select list queries we need to create a dictionary of alias to group by value.
            /// For each grouping drained from the backend we merge it with the results here.
            /// At the end this class will form a JSON object with the correct aliases and grouping result.
            /// </summary>
            private sealed class SelectListGroupByValues : GroupByValues
            {
                private readonly IReadOnlyDictionary<string, GroupByValue> aliasToValue;

                private SelectListGroupByValues(IReadOnlyDictionary<string, GroupByValue> aliasToValue)
                {
                    this.aliasToValue = aliasToValue;
                }

                public override CosmosElement GetResult()
                {
                    Dictionary<string, CosmosElement> mergedResult = new Dictionary<string, CosmosElement>();
                    foreach (KeyValuePair<string, GroupByValue> aliasAndValue in this.aliasToValue)
                    {
                        string alias = aliasAndValue.Key;
                        GroupByValue groupByValue = aliasAndValue.Value;
                        mergedResult[alias] = groupByValue.Result;
                    }

                    return CosmosObject.Create(mergedResult);
                }

                public static SelectListGroupByValues Create(IReadOnlyDictionary<string, AggregateOperator?> groupByAliasToAggregateType)
                {
                    Dictionary<string, GroupByValue> groupingTable = new Dictionary<string, GroupByValue>();
                    foreach (KeyValuePair<string, AggregateOperator?> aliasToAggregate in groupByAliasToAggregateType)
                    {
                        string alias = aliasToAggregate.Key;
                        AggregateOperator? aggregateOperator = aliasToAggregate.Value;
                        groupingTable[alias] = GroupByValue.Create(aggregateOperator);
                    }

                    return new SelectListGroupByValues(groupingTable);
                }

                public override void AddValues(CosmosElement values)
                {
                    if (!(values is CosmosObject payload))
                    {
                        throw new ArgumentException("values is not an object.");
                    }

                    foreach (KeyValuePair<string, GroupByValue> aliasAndValue in this.aliasToValue)
                    {
                        string alias = aliasAndValue.Key;
                        GroupByValue groupByValue = aliasAndValue.Value;
                        groupByValue.AddValue(payload[alias]);
                    }
                }

                public override string ToString()
                {
                    return JsonConvert.SerializeObject(this.aliasToValue);
                }
            }
        }

        /// <summary>
        /// With a group by value we need to encapsulate the fact that we have:
        /// 1) aggregate group by values
        /// 2) scalar group by values.
        /// </summary>
        private abstract class GroupByValue
        {
            public abstract void AddValue(CosmosElement groupByValue);

            public abstract CosmosElement Result { get; }

            public override string ToString()
            {
                return this.Result.ToString();
            }

            public static GroupByValue Create(AggregateOperator? aggregateOperator)
            {
                GroupByValue value;
                if (aggregateOperator.HasValue)
                {
                    value = AggregateGroupByValue.Create(aggregateOperator.Value);
                }
                else
                {
                    value = ScalarGroupByValue.Create();
                }

                return value;
            }

            private sealed class AggregateGroupByValue : GroupByValue
            {
                private readonly IAggregator aggregator;

                public override CosmosElement Result
                {
                    get
                    {
                        return this.aggregator.GetResult();
                    }
                }

                private AggregateGroupByValue(IAggregator aggregator)
                {
                    if (aggregator == null)
                    {
                        throw new ArgumentNullException(nameof(aggregator));
                    }

                    this.aggregator = aggregator;
                }

                public override void AddValue(CosmosElement groupByValue)
                {
                    AggregateItem aggregateItem = new AggregateItem(groupByValue);
                    this.aggregator.Aggregate(aggregateItem.Item);
                }

                public static AggregateGroupByValue Create(AggregateOperator aggregateOperator)
                {
                    IAggregator aggregator;
                    switch (aggregateOperator)
                    {
                        case AggregateOperator.Average:
                            aggregator = new AverageAggregator();
                            break;

                        case AggregateOperator.Count:
                            aggregator = new CountAggregator();
                            break;

                        case AggregateOperator.Max:
                            aggregator = new MinMaxAggregator(isMinAggregation: false);
                            break;

                        case AggregateOperator.Min:
                            aggregator = new MinMaxAggregator(isMinAggregation: true);
                            break;

                        case AggregateOperator.Sum:
                            aggregator = new SumAggregator();
                            break;

                        default:
                            throw new ArgumentException($"Unknown {nameof(AggregateOperator)}: {aggregateOperator}.");
                    }

                    return new AggregateGroupByValue(aggregator);
                }
            }

            private sealed class ScalarGroupByValue : GroupByValue
            {
                private CosmosElement value;
                private bool initialized;

                private ScalarGroupByValue()
                {
                    this.value = null;
                    this.initialized = false;
                }

                public override CosmosElement Result
                {
                    get
                    {
                        if (!this.initialized)
                        {
                            throw new InvalidOperationException($"{nameof(ScalarGroupByValue)} is not yet initialized.");
                        }

                        return this.value;
                    }
                }

                public static ScalarGroupByValue Create()
                {
                    return new ScalarGroupByValue();
                }

                public override void AddValue(CosmosElement groupByValue)
                {
                    if (!this.initialized)
                    {
                        this.value = groupByValue;
                        this.initialized = true;
                    }
                }
            }
        }
    }
}
