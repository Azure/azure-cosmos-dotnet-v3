//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Aggregation
{
    using System;
    using Microsoft.Azure.Cosmos.CosmosElements;

    /// <summary>
    /// Concrete implementation of IAggregator that can take the global weighted average from the local weighted average of multiple partitions and continuations.
    /// The way this works is that for each continuation in each partition we decompose the average into a sum and count.
    /// Let us denote the sum and count for the ith continuation in the jth partition as (sum_i,j, count_i,j),
    /// then the true average for the whole query is SUM(sum_i,j for all i and all j) / SUM(count_i,j for all i and all j),
    /// this way the average is weighted across continuation and partitions that have more or less documents contributing to their average.
    /// </summary>
    internal sealed class AverageAggregator : IAggregator
    {
        /// <summary>
        /// The running weighted average for this aggregator.
        /// </summary>
        private AverageInfo globalAverage = new AverageInfo(0, 0);

        /// <summary>
        /// Averages the supplied item with the previously supplied items.
        /// </summary>
        /// <param name="localAverage">The local average to add to the global average.</param>
        public void Aggregate(CosmosElement localAverage)
        {
            // item is a JObject of the form : { "sum": <number>, "count": <number> } 
            AverageInfo newInfo = AverageInfo.Create(localAverage);
            this.globalAverage += newInfo;
        }

        /// <summary>
        /// Returns the current running average or undefined if any of the intermediate averages resulted in an undefined value.
        /// </summary>
        /// <returns>The current running average or undefined if any of the intermediate averages resulted in an undefined value.</returns>
        public CosmosElement GetResult()
        {
            return this.globalAverage.GetAverage();
        }

        /// <summary>
        /// Struct that stores a weighted average as a sum and count so they that average across different partitions with different numbers of documents can be taken.
        /// </summary>
        private struct AverageInfo
        {
            private const string SumName = "sum";
            private const string CountName = "count";

            /// <summary>
            /// Initializes a new instance of the AverageInfo class.
            /// </summary>
            /// <param name="sum">The sum (if defined).</param>
            /// <param name="count">The count.</param>
            public AverageInfo(double? sum, long count)
            {
                this.Sum = sum;
                this.Count = count;
            }

            /// <summary>
            /// Initializes a new instance of the AverageInfo class.
            /// </summary>
            public static AverageInfo Create(CosmosElement cosmosElement)
            {
                if (cosmosElement == null)
                {
                    throw new ArgumentNullException($"{nameof(cosmosElement)} must not be null.");
                }

                if (!(cosmosElement is CosmosObject cosmosObject))
                {
                    throw new ArgumentException($"{nameof(cosmosElement)} must not be an object.");
                }

                double? sum;
                if (cosmosObject.TryGetValue(SumName, out CosmosElement sumPropertyValue))
                {
                    if (!(sumPropertyValue is CosmosNumber cosmosSum))
                    {
                        throw new ArgumentException($"value for the {SumName} field was not a number");
                    }

                    if (cosmosSum.IsFloatingPoint)
                    {
                        sum = cosmosSum.AsFloatingPoint().Value;
                    }
                    else
                    {
                        sum = cosmosSum.AsInteger().Value;
                    }
                }
                else
                {
                    sum = null;
                }

                long count;
                if (!cosmosObject.TryGetValue(CountName, out CosmosElement countPropertyValue))
                {
                    throw new ArgumentException($"object did not have property name {CountName}");
                }

                if (!(countPropertyValue is CosmosNumber cosmosCount))
                {
                    throw new ArgumentException($"value for the {CountName} field was not a number");
                }

                if (cosmosCount.IsFloatingPoint)
                {
                    count = (long)cosmosCount.AsFloatingPoint().Value;
                }
                else
                {
                    count = cosmosCount.AsInteger().Value;
                }

                return new AverageInfo(sum, count);
            }

            /// <summary>
            /// Gets the some component of the weighted average (or null of the result is undefined).
            /// </summary>
            public double? Sum
            {
                get;
            }

            /// <summary>
            /// Gets the count component of the weighted average.
            /// </summary>
            public long Count
            {
                get;
            }

            /// <summary>
            /// Takes the sum of two AverageInfo structs
            /// </summary>
            /// <param name="info1">The first AverageInfo.</param>
            /// <param name="info2">The second AverageInfo.</param>
            /// <returns>The sum of two AverageInfo structs</returns>
            public static AverageInfo operator +(AverageInfo info1, AverageInfo info2)
            {
                // For a query taking the average of a items where any of the items is not a number results in Undefined / 0 documents.
                // We replicated that here by checking if the sum has a value.
                if (!info1.Sum.HasValue || !info2.Sum.HasValue)
                {
                    return new AverageInfo(null, info1.Count + info2.Count);
                }

                return new AverageInfo(info1.Sum + info2.Sum, info1.Count + info2.Count);
            }

            /// <summary>
            /// Returns the average or undefined if any of the intermediate averages resulted in an undefined value.
            /// </summary>
            /// <returns>The average or undefined if any of the intermediate averages resulted in an undefined value.</returns>
            public CosmosNumber GetAverage()
            {
                if (!this.Sum.HasValue || this.Count <= 0)
                {
                    return null;
                }

                return CosmosNumber64.Create(this.Sum.Value / this.Count);
            }
        }
    }
}