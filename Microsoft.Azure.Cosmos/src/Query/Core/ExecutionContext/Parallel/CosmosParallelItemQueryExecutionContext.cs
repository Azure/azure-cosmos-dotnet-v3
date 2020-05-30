//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.Parallel
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.ItemProducers;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;

    /// <summary>
    /// CosmosParallelItemQueryExecutionContext is a concrete implementation for CrossPartitionQueryExecutionContext.
    /// This class is responsible for draining cross partition queries that do not have order by conditions.
    /// The way parallel queries work is that it drains from the left most partition first.
    /// This class handles draining in the correct order and can also stop and resume the query 
    /// by generating a continuation token and resuming from said continuation token.
    /// </summary>
    internal sealed partial class CosmosParallelItemQueryExecutionContext : CosmosCrossPartitionQueryExecutionContext
    {
        /// <summary>
        /// The comparer used to determine, which continuation tokens should be returned to the user.
        /// </summary>
        private static readonly IEqualityComparer<CosmosElement> EqualityComparer = new ParallelEqualityComparer();

        /// <summary>
        /// The function to determine which partition to fetch from first.
        /// </summary>
        private static readonly Func<ItemProducerTree, int> FetchPriorityFunction = documentProducerTree => int.Parse(documentProducerTree.PartitionKeyRange.Id);

        private readonly bool returnResultsInDeterministicOrder;
        
        /// <summary>
        /// Initializes a new instance of the CosmosParallelItemQueryExecutionContext class.
        /// </summary>
        /// <param name="queryContext">The parameters for constructing the base class.</param>
        /// <param name="maxConcurrency">The max concurrency</param>
        /// <param name="maxBufferedItemCount">The max buffered item count</param>
        /// <param name="maxItemCount">Max item count</param>
        /// <param name="moveNextComparer">The comparer to use for the priority queue.</param>
        /// <param name="returnResultsInDeterministicOrder">Whether or not to return results in deterministic order.</param>
        /// <param name="tryFillPageFully">Tries to fill the page fully.</param>
        /// <param name="testSettings">Test settings.</param>
        private CosmosParallelItemQueryExecutionContext(
            CosmosQueryContext queryContext,
            int? maxConcurrency,
            int? maxItemCount,
            int? maxBufferedItemCount,
            IComparer<ItemProducerTree> moveNextComparer,
            bool returnResultsInDeterministicOrder,
            bool tryFillPageFully,
            TestInjections testSettings)
            : base(
                queryContext: queryContext,
                maxConcurrency: maxConcurrency,
                maxItemCount: maxItemCount,
                maxBufferedItemCount: maxBufferedItemCount,
                moveNextComparer: moveNextComparer,
                fetchPrioirtyFunction: CosmosParallelItemQueryExecutionContext.FetchPriorityFunction,
                equalityComparer: CosmosParallelItemQueryExecutionContext.EqualityComparer,
                returnResultsInDeterministicOrder: returnResultsInDeterministicOrder,
                tryFillPageFully: tryFillPageFully,
                testSettings: testSettings)
        {
            this.returnResultsInDeterministicOrder = returnResultsInDeterministicOrder;
        }
    }
}
