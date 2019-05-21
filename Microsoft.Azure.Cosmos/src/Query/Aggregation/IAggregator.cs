//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Aggregation
{
    using Microsoft.Azure.Cosmos.CosmosElements;

    /// <summary>
    /// Interface for all aggregators that are used to aggregate across continuation and partition boundaries.
    /// </summary>
    internal interface IAggregator
    {
        /// <summary>
        /// Adds an item to the aggregation.
        /// </summary>
        /// <param name="item">The item to add to the aggregation.</param>
        void Aggregate(CosmosElement item);

        /// <summary>
        /// Gets the result of the aggregation.
        /// </summary>
        /// <returns>The result of the aggregation.</returns>
        CosmosElement GetResult();
    }
}
