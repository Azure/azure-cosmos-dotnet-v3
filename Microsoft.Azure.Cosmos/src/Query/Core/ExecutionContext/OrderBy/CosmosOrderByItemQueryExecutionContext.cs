// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.OrderBy
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.ItemProducers;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;

    /// <summary>
    /// CosmosOrderByItemQueryExecutionContext is a concrete implementation for CrossPartitionQueryExecutionContext.
    /// This class is responsible for draining cross partition queries that have order by conditions.
    /// The way order by queries work is that they are doing a k-way merge of sorted lists from each partition with an added condition.
    /// The added condition is that if 2 or more top documents from different partitions are equivalent then we drain from the left most partition first.
    /// This way we can generate a single continuation token for all n partitions.
    /// This class is able to stop and resume execution by generating continuation tokens and reconstructing an execution context from said token.
    /// </summary>
    internal sealed partial class CosmosOrderByItemQueryExecutionContext
    {
        /// <summary>
        /// Order by queries are rewritten to allow us to inject a filter.
        /// This placeholder is so that we can just string replace it with the filter we want without having to understand the structure of the query.
        /// </summary>
        private const string FormatPlaceHolder = "{documentdb-formattableorderbyquery-filter}";

        /// <summary>
        /// If query does not need a filter then we replace the FormatPlaceHolder with "true", since
        /// "SELECT * FROM c WHERE blah and true" is the same as "SELECT * FROM c where blah"
        /// </summary>
        private const string True = "true";

        /// <summary>
        /// Function to determine the priority of fetches.
        /// Basically we are fetching from the partition with the least number of buffered documents first.
        /// </summary>
        private static readonly Func<ItemProducerTree, int> FetchPriorityFunction = itemProducerTree => itemProducerTree.BufferedItemCount;

        /// <summary>
        /// Skip count used for JOIN queries.
        /// You can read up more about this in the documentation for the continuation token.
        /// </summary>
        private int skipCount;

        /// <summary>
        /// We need to keep track of the previousRid, since order by queries don't drain full pages.
        /// </summary>
        private string previousRid;

        private IReadOnlyList<OrderByItem> previousOrderByItems;

        /// <summary>
        /// Initializes a new instance of the CosmosOrderByItemQueryExecutionContext class.
        /// </summary>
        /// <param name="initPararms">The params used to construct the base class.</param>
        /// For cross partition order by queries a query like "SELECT c.id, c.field_0 ORDER BY r.field_7 gets rewritten as:
        /// <![CDATA[
        /// SELECT r._rid, [{"item": r.field_7}] AS orderByItems, {"id": r.id, "field_0": r.field_0} AS payload
        /// FROM r
        /// WHERE({ document db - formattable order by query - filter})
        /// ORDER BY r.field_7]]>
        /// This is needed because we need to add additional filters to the query when we resume from a continuation,
        /// and it lets us easily parse out the _rid orderByItems, and payload without parsing the entire document (and having to remember the order by field).
        /// <param name="maxConcurrency">The max concurrency</param>
        /// <param name="maxBufferedItemCount">The max buffered item count</param>
        /// <param name="maxItemCount">Max item count</param>
        /// <param name="consumeComparer">Comparer used to internally compare documents from different sorted partitions.</param>
        /// <param name="testSettings">Test settings.</param>
        private CosmosOrderByItemQueryExecutionContext(
            CosmosQueryContext initPararms,
            int? maxConcurrency,
            int? maxItemCount,
            int? maxBufferedItemCount,
            OrderByItemProducerTreeComparer consumeComparer,
            TestInjections testSettings)
            : base(
                queryContext: initPararms,
                maxConcurrency: maxConcurrency,
                maxItemCount: maxItemCount,
                maxBufferedItemCount: maxBufferedItemCount,
                moveNextComparer: consumeComparer,
                fetchPrioirtyFunction: CosmosOrderByItemQueryExecutionContext.FetchPriorityFunction,
                equalityComparer: new OrderByEqualityComparer(consumeComparer),
                returnResultsInDeterministicOrder: true,
                testSettings: testSettings)
        {
        }
    }
}
