// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.GroupBy
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Aggregate;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Aggregate.Aggregators;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Distinct;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;

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
    internal class GroupByQueryPipelineStage : QueryPipelineStageBase
    {
        private const string ContinuationTokenNotSupportedWithGroupBy = "Continuation token is not supported for queries with GROUP BY. Do not use FeedResponse.ResponseContinuation or remove the GROUP BY from the query.";

        private readonly GroupingTable groupingTable;
        protected readonly int pageSize;
        protected bool returnedLastPage; 

        protected GroupByQueryPipelineStage(
            IQueryPipelineStage source,
            GroupingTable groupingTable,
            int pageSize)
            : base(source)
        {
            this.groupingTable = groupingTable ?? throw new ArgumentNullException(nameof(groupingTable));
            this.pageSize = pageSize;
        }

        public static TryCatch<IQueryPipelineStage> MonadicCreate(
            CosmosElement requestContinuation,
            MonadicCreatePipelineStage monadicCreatePipelineStage,
            IReadOnlyList<AggregateOperator> aggregates,
            IReadOnlyDictionary<string, AggregateOperator?> groupByAliasToAggregateType,
            IReadOnlyList<string> orderedAliases,
            bool hasSelectValue,
            int pageSize)
        {
            TryCatch<GroupingTable> tryCreateGroupingTable = GroupingTable.TryCreateFromContinuationToken(
                aggregates,
                groupByAliasToAggregateType,
                orderedAliases,
                hasSelectValue,
                continuationToken: null);

            if (tryCreateGroupingTable.Failed)
            {
                return TryCatch<IQueryPipelineStage>.FromException(tryCreateGroupingTable.Exception);
            }

            TryCatch<IQueryPipelineStage> tryCreateSource = monadicCreatePipelineStage(requestContinuation);
            if (tryCreateSource.Failed)
            {
                return tryCreateSource;
            }

            IQueryPipelineStage stage = new GroupByQueryPipelineStage(
                tryCreateSource.Result,
                tryCreateGroupingTable.Result,
                pageSize);

            return TryCatch<IQueryPipelineStage>.FromResult(stage);
        }

        protected void AggregateGroupings(IReadOnlyList<CosmosElement> cosmosElements)
        {
            foreach (CosmosElement result in cosmosElements)
            {
                // Aggregate the values for all groupings across all continuations.
                RewrittenGroupByProjection groupByItem = new RewrittenGroupByProjection(result);
                this.groupingTable.AddPayload(groupByItem);
            }
        }

        public override async ValueTask<bool> MoveNextAsync(Tracing.ITrace trace, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

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
            IReadOnlyDictionary<string, string> addtionalHeaders = null;

            while (await this.inputStage.MoveNextAsync(trace, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

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
                cosmosQueryExecutionInfo: default,
                distributionPlanSpec: default,
                disallowContinuationTokenMessage: GroupByQueryPipelineStage.ContinuationTokenNotSupportedWithGroupBy,
                additionalHeaders: addtionalHeaders,
                state: default,
                streaming: null);

            this.Current = TryCatch<QueryPage>.FromResult(queryPage);
            return true;
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
        protected readonly struct RewrittenGroupByProjection
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

            public bool TryGetPayload(out CosmosElement payload)
            {
                if (!this.cosmosObject.TryGetValue(PayloadPropertyName, out payload))
                {
                    payload = CosmosUndefined.Create();
                }

                return true;
            }
        }

        protected sealed class GroupingTable : IEnumerable<KeyValuePair<UInt128, SingleGroupAggregator>>
        {
            private static readonly IReadOnlyList<AggregateOperator> EmptyAggregateOperators = new AggregateOperator[] { };

            private readonly Dictionary<UInt128, SingleGroupAggregator> table;
            private readonly IReadOnlyList<AggregateOperator> aggregates;
            private readonly IReadOnlyDictionary<string, AggregateOperator?> groupByAliasToAggregateType;
            private readonly IReadOnlyList<string> orderedAliases;
            private readonly bool hasSelectValue;

            private GroupingTable(
                IReadOnlyList<AggregateOperator> aggregates,
                IReadOnlyDictionary<string, AggregateOperator?> groupByAliasToAggregateType,
                IReadOnlyList<string> orderedAliases,
                bool hasSelectValue)
            {
                this.aggregates = aggregates ?? throw new ArgumentNullException(nameof(aggregates));
                this.groupByAliasToAggregateType = groupByAliasToAggregateType ?? throw new ArgumentNullException(nameof(groupByAliasToAggregateType));
                this.orderedAliases = orderedAliases;
                this.hasSelectValue = hasSelectValue;
                this.table = new Dictionary<UInt128, SingleGroupAggregator>();
            }

            public int Count => this.table.Count;

            public bool IsDone { get; private set; }

            public void AddPayload(RewrittenGroupByProjection rewrittenGroupByProjection)
            {
                // For VALUE queries the payload will be undefined if the field was undefined.
                if (rewrittenGroupByProjection.TryGetPayload(out CosmosElement payload))
                {
                    UInt128 groupByKeysHash = DistinctHash.GetHash(rewrittenGroupByProjection.GroupByItems);

                    if (!this.table.TryGetValue(groupByKeysHash, out SingleGroupAggregator singleGroupAggregator))
                    {
                        singleGroupAggregator = SingleGroupAggregator.TryCreate(
                            this.aggregates,
                            this.groupByAliasToAggregateType,
                            this.orderedAliases,
                            this.hasSelectValue,
                            continuationToken: null).Result;
                        this.table[groupByKeysHash] = singleGroupAggregator;
                    }

                    singleGroupAggregator.AddValues(payload);
                }
            }

            public IReadOnlyList<CosmosElement> Drain(int maxItemCount)
            {
                List<UInt128> keys = this.table.Keys.Take(maxItemCount).ToList();
                List<SingleGroupAggregator> singleGroupAggregators = new List<SingleGroupAggregator>(keys.Count);
                foreach (UInt128 key in keys)
                {
                    SingleGroupAggregator singleGroupAggregator = this.table[key];
                    singleGroupAggregators.Add(singleGroupAggregator);
                }

                foreach (UInt128 key in keys)
                {
                    this.table.Remove(key);
                }

                List<CosmosElement> results = new List<CosmosElement>();
                foreach (SingleGroupAggregator singleGroupAggregator in singleGroupAggregators)
                {
                        results.Add(singleGroupAggregator.GetResult());
                }

                if (this.Count == 0)
                {
                    this.IsDone = true;
                }

                return results;
            }

            public IEnumerator<KeyValuePair<UInt128, SingleGroupAggregator>> GetEnumerator => this.table.GetEnumerator();

            public static TryCatch<GroupingTable> TryCreateFromContinuationToken(
                IReadOnlyList<AggregateOperator> aggregates,
                IReadOnlyDictionary<string, AggregateOperator?> groupByAliasToAggregateType,
                IReadOnlyList<string> orderedAliases,
                bool hasSelectValue,
                CosmosElement continuationToken)
            {
                GroupingTable groupingTable = new GroupingTable(
                    aggregates,
                    groupByAliasToAggregateType,
                    orderedAliases,
                    hasSelectValue);

                if (continuationToken != null)
                {
                    if (!(continuationToken is CosmosObject groupingTableContinuationToken))
                    {
                        return TryCatch<GroupingTable>.FromException(
                            new MalformedContinuationTokenException(
                                $"Invalid GroupingTableContinuationToken"));
                    }

                    foreach (KeyValuePair<string, CosmosElement> kvp in groupingTableContinuationToken)
                    {
                        string key = kvp.Key;
                        CosmosElement value = kvp.Value;

                        if (!UInt128.TryParse(key, out UInt128 groupByKey))
                        {
                            return TryCatch<GroupingTable>.FromException(
                                new MalformedContinuationTokenException(
                                    $"Invalid GroupingTableContinuationToken"));
                        }

                        TryCatch<SingleGroupAggregator> tryCreateSingleGroupAggregator = SingleGroupAggregator.TryCreate(
                            aggregates,
                            groupByAliasToAggregateType,
                            orderedAliases,
                            hasSelectValue,
                            value);

                        if (tryCreateSingleGroupAggregator.Succeeded)
                        {
                            groupingTable.table[groupByKey] = tryCreateSingleGroupAggregator.Result;
                        }
                        else
                        {
                            return TryCatch<GroupingTable>.FromException(tryCreateSingleGroupAggregator.Exception);
                        }
                    }
                }

                return TryCatch<GroupingTable>.FromResult(groupingTable);
            }

            IEnumerator<KeyValuePair<UInt128, SingleGroupAggregator>> IEnumerable<KeyValuePair<UInt128, SingleGroupAggregator>>.GetEnumerator()
            {
                return this.table.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.table.GetEnumerator();
            }
        }
    }
}
