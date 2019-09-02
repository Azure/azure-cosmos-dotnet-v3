//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Aggregation
{
    using System;
    using Microsoft.Azure.Cosmos.CosmosElements;

    /// <summary>
    /// Concrete implementation of IAggregator that can take the global sum from the local sum of multiple partitions and continuations.
    /// Let sum_i,j be the sum from the ith continuation in the jth partition, 
    /// then the sum for the entire query is SUM(sum_i,j for all i and j).
    /// </summary>
    internal sealed class SumAggregator : IAggregator
    {
        /// <summary>
        /// The global sum.
        /// </summary>
        private double globalSum;

        /// <summary>
        /// Adds a local sum to the global sum.
        /// </summary>
        /// <param name="localSum">The local sum.</param>
        public void Aggregate(CosmosElement localSum)
        {
            // If someone tried to add an undefined just set the globalSum to NaN and it will stay that way for the duration of the aggregation.
            if (localSum == null)
            {
                this.globalSum = double.NaN;
            }
            else
            {
                if (!(localSum is CosmosNumber cosmosNumber))
                {
                    throw new ArgumentException("localSum must be a number.");
                }

                if (cosmosNumber.IsFloatingPoint)
                {
                    this.globalSum += cosmosNumber.AsFloatingPoint().Value;
                }
                else
                {
                    this.globalSum += cosmosNumber.AsInteger().Value;
                }
            }
        }

        /// <summary>
        /// Gets the current sum.
        /// </summary>
        /// <returns>The current sum.</returns>
        public CosmosElement GetResult()
        {
            if (double.IsNaN(this.globalSum))
            {
                return null;
            }

            return CosmosNumber64.Create(this.globalSum);
        }
    }
}
