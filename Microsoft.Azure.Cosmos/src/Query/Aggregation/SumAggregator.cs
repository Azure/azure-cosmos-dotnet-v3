//-----------------------------------------------------------------------
// <copyright file="SumAggregator.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Aggregation
{
    using System;
    using System.Globalization;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Internal;

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
                switch (localSum)
                {
                    case CosmosNumber cosmosNumber:
                    {
                        if (cosmosNumber.IsFloatingPoint)
                        {
                            this.globalSum += cosmosNumber.AsFloatingPoint().Value;
                        }
                        else
                        {
                            this.globalSum += cosmosNumber.AsInteger().Value;
                        }

                        break;
                    }

                    case CosmosTypedElement<sbyte> int8number:
                    {
                        this.globalSum += int8number.Value;
                        break;
                    }

                    case CosmosTypedElement<short> number:
                    {
                        this.globalSum += number.Value;
                        break;
                    }

                    case CosmosTypedElement<int> number:
                    {
                        this.globalSum += number.Value;
                        break;
                    }

                    case CosmosTypedElement<long> number:
                    {
                        this.globalSum += number.Value;
                        break;
                    }

                    case CosmosTypedElement<uint> number:
                    {
                        this.globalSum += number.Value;
                        break;
                    }

                    case CosmosTypedElement<float> number:
                    {
                        this.globalSum += number.Value;
                        break;
                    }

                    case CosmosTypedElement<double> number:
                    {
                        this.globalSum += number.Value;
                        break;
                    }

                    default:
                        throw new ArgumentException("localSum must be a number.");
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

            return CosmosNumber.Create(this.globalSum);
        }
    }
}
