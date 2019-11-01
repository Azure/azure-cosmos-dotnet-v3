//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;

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
    internal abstract partial class GroupByDocumentQueryExecutionComponent : DocumentQueryExecutionComponentBase
    {
        private static readonly AggregateOperator[] EmptyAggregateOperators = new AggregateOperator[] { };

        private readonly CosmosQueryClient cosmosQueryClient;
        private readonly IReadOnlyDictionary<string, AggregateOperator?> groupByAliasToAggregateType;
        private readonly IReadOnlyList<string> orderedAliases;
        private readonly Dictionary<UInt192, SingleGroupAggregator> groupingTable;
        private readonly bool hasSelectValue;

        private int numPagesDrainedFromGroupingTable;
        private bool isDone;

        protected GroupByDocumentQueryExecutionComponent(
            IDocumentQueryExecutionComponent source,
            CosmosQueryClient cosmosQueryClient,
            IReadOnlyDictionary<string, AggregateOperator?> groupByAliasToAggregateType,
            IReadOnlyList<string> orderedAliases,
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

            this.groupingTable = new Dictionary<UInt192, SingleGroupAggregator>();

            this.cosmosQueryClient = cosmosQueryClient;
            this.groupByAliasToAggregateType = groupByAliasToAggregateType;
            this.orderedAliases = orderedAliases;
            this.hasSelectValue = hasSelectValue;
            this.numPagesDrainedFromGroupingTable = numPagesDrainedFromGroupingTable;

            if (groupingTableContinuationToken != null)
            {
                if (!CosmosElement.TryParse<CosmosObject>(
                    groupingTableContinuationToken,
                    out CosmosObject parsedGroupingTableContinuations))
                {
                    throw this.cosmosQueryClient.CreateBadRequestException($"Invalid GroupingTableContinuationToken");
                }

                foreach (KeyValuePair<string, CosmosElement> kvp in parsedGroupingTableContinuations)
                {
                    string key = kvp.Key;
                    CosmosElement value = kvp.Value;

                    UInt192 groupByKey = UInt192.Parse(key);

                    if (!(value is CosmosString singleGroupAggregatorContinuationToken))
                    {
                        throw this.cosmosQueryClient.CreateBadRequestException($"Invalid GroupingTableContinuationToken");
                    }
                    SingleGroupAggregator singleGroupAggregator = SingleGroupAggregator.Create(
                        this.cosmosQueryClient,
                        EmptyAggregateOperators,
                        this.groupByAliasToAggregateType,
                        this.orderedAliases,
                        this.hasSelectValue,
                        singleGroupAggregatorContinuationToken.Value);

                    this.groupingTable[groupByKey] = singleGroupAggregator;
                }
            }
        }

        public override bool IsDone => this.isDone;

        public static async Task<IDocumentQueryExecutionComponent> CreateAsync(
            ExecutionEnvironment executionEnvironment,
            CosmosQueryClient cosmosQueryClient,
            string requestContinuation,
            Func<string, Task<IDocumentQueryExecutionComponent>> createSourceCallback,
            IReadOnlyDictionary<string, AggregateOperator?> groupByAliasToAggregateType,
            IReadOnlyList<string> orderedAliases,
            bool hasSelectValue)
        {
            IDocumentQueryExecutionComponent groupByDocumentQueryExecutionComponent;
            switch (executionEnvironment)
            {
                case ExecutionEnvironment.Client:
                    groupByDocumentQueryExecutionComponent = await ClientGroupByDocumentQueryExecutionComponent.CreateAsync(
                        cosmosQueryClient,
                        requestContinuation,
                        createSourceCallback,
                        groupByAliasToAggregateType,
                        orderedAliases,
                        hasSelectValue);
                    break;

                case ExecutionEnvironment.Compute:
                    groupByDocumentQueryExecutionComponent = await ComputeGroupByDocumentQueryExecutionComponent.CreateAsync(
                        cosmosQueryClient,
                        requestContinuation,
                        createSourceCallback,
                        groupByAliasToAggregateType,
                        orderedAliases,
                        hasSelectValue);
                    break;

                default:
                    throw new ArgumentException($"Unknown {nameof(ExecutionEnvironment)}: {executionEnvironment}.");
            }

            return groupByDocumentQueryExecutionComponent;
        }

        protected void AggregateGroupings(IReadOnlyList<CosmosElement> cosmosElements)
        {
            foreach (CosmosElement result in cosmosElements)
            {
                // Aggregate the values for all groupings across all continuations.
                RewrittenGroupByProjection groupByItem = new RewrittenGroupByProjection(result);
                UInt192 groupByKeysHash = DistinctHash.GetHash(groupByItem.GroupByItems);

                if (!this.groupingTable.TryGetValue(groupByKeysHash, out SingleGroupAggregator singleGroupAggregator))
                {
                    singleGroupAggregator = SingleGroupAggregator.Create(
                        this.cosmosQueryClient,
                        EmptyAggregateOperators,
                        this.groupByAliasToAggregateType,
                        this.orderedAliases,
                        this.hasSelectValue,
                        continuationToken: null);
                    this.groupingTable[groupByKeysHash] = singleGroupAggregator;
                }

                CosmosElement payload = groupByItem.Payload;
                singleGroupAggregator.AddValues(payload);
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
    }
}