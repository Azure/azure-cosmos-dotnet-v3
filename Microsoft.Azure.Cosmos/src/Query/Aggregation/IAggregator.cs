//-----------------------------------------------------------------------
// <copyright file="IAggregator.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Aggregation
{
    /// <summary>
    /// Interface for all aggregators that are used to aggregate across continuation and partition boundaries.
    /// </summary>
    internal interface IAggregator
    {
        /// <summary>
        /// Adds an item to the aggregation.
        /// </summary>
        /// <param name="item">The item to add to the aggregation.</param>
        void Aggregate(object item);

        /// <summary>
        /// Gets the result of the aggregation.
        /// </summary>
        /// <returns>The result of the aggregation.</returns>
        object GetResult();
    }
}
