//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Aggregate.Aggregators
{
    using System;
    using System.Globalization;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    /// <summary>
    /// Concrete implementation of IAggregator that can take the global count from the local counts from multiple partitions and continuations.
    /// Let count_i,j be the count from the ith continuation in the jth partition, 
    /// then the count for the entire query is SUM(count_i,j for all i and j)
    /// </summary>
    internal sealed class CountAggregator : IAggregator
    {
        /// <summary>
        /// The global count.
        /// </summary>
        private long globalCount;

        private CountAggregator(long initialCount)
        {
            if (initialCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCount));
            }

            this.globalCount = initialCount;
        }

        /// <summary>
        /// Adds a count to the running count.
        /// </summary>
        /// <param name="localCount">The count to add.</param>
        public void Aggregate(CosmosElement localCount)
        {
            if (!(localCount is CosmosNumber cosmosNumber))
            {
                throw new ArgumentException($"{nameof(localCount)} must be a number.");
            }

            this.globalCount += Number64.ToLong(cosmosNumber.Value);
        }

        /// <summary>
        /// Gets the global count.
        /// </summary>
        /// <returns>The global count.</returns>
        public CosmosElement GetResult()
        {
            return CosmosNumber64.Create(this.globalCount);
        }

        public static TryCatch<IAggregator> TryCreate(CosmosElement continuationToken)
        {
            long partialCount;
            if (continuationToken != null)
            {
                if (!(continuationToken is CosmosNumber cosmosPartialCount))
                {
                    return TryCatch<IAggregator>.FromException(
                        new MalformedContinuationTokenException($@"Invalid count continuation token: ""{continuationToken}""."));
                }

                partialCount = Number64.ToLong(cosmosPartialCount.Value);
            }
            else
            {
                partialCount = 0;
            }

            return TryCatch<IAggregator>.FromResult(
                new CountAggregator(initialCount: partialCount));
        }
    }
}