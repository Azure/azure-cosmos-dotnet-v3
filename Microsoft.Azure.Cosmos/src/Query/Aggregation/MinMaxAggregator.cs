//-----------------------------------------------------------------------
// <copyright file="MinMaxAggregator.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Aggregation
{
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Internal;

    /// <summary>
    /// Concrete implementation of IAggregator that can take the global min/max from the local min/max of multiple partitions and continuations.
    /// Let min/max_i,j be the min/max from the ith continuation in the jth partition, 
    /// then the min/max for the entire query is MIN/MAX(min/max_i,j for all i and j).
    /// </summary>
    internal sealed class MinMaxAggregator : IAggregator
    {
        /// <summary>
        /// Whether or not the aggregation is a min or a max.
        /// </summary>
        private readonly bool isMinAggregation;
        
        /// <summary>
        /// The global max of all items seen.
        /// </summary>
        private object globalMinMax;

        public MinMaxAggregator(bool isMinAggregation)
        {
            this.isMinAggregation = isMinAggregation;
            if (this.isMinAggregation)
            {
                globalMinMax = ItemComparer.MaxValue;
            }
            else
            {
                globalMinMax = ItemComparer.MinValue;
            }
        }

        public void Aggregate(CosmosElement localMinMax)
        {
            // If the value became undefinded at some point then it should stay that way.
            if (this.globalMinMax == Undefined.Value)
            {
                return;
            }

            if (localMinMax == null)
            {
                // If we got an undefined in the pipeline then the whole thing becomes undefined.
                this.globalMinMax = Undefined.Value;
                return;
            }

            object unwrappedLocalMinMax = localMinMax.ToObject();
            // Check to see if we got the higher precision result 
            // and unwrap the object to get the actual item of interest
            if (localMinMax is CosmosObject cosmosObject)
            {
                if (cosmosObject["count"] is CosmosNumber countToken)
                {
                    // We know the object looks like: {"min": MIN(c.blah), "count": COUNT(c.blah)}
                    if (countToken.AsInteger().Value == 0)
                    {
                        // Ignore the value since the continuation / partition had no results that matched the filter so min is undefined.
                        return;
                    }

                    CosmosElement min = cosmosObject["min"];
                    CosmosElement max = cosmosObject["max"];

                    // Note that JToken won't equal null as long as a value is there
                    // even if that value is a JSON null.
                    if (min != null)
                    {
                        unwrappedLocalMinMax = min.ToObject();
                    }
                    else if (max != null)
                    {
                        unwrappedLocalMinMax = max.ToObject();
                    }
                    else
                    {
                        unwrappedLocalMinMax = Undefined.Value;
                    }
                }
            }

            if (!ItemComparer.IsMinOrMax(this.globalMinMax) 
                && (!ItemTypeHelper.IsPrimitive(unwrappedLocalMinMax) || !ItemTypeHelper.IsPrimitive(this.globalMinMax)))
            {
                // This means we are comparing non primitives with is undefined
                this.globalMinMax = Undefined.Value;
                return;
            }

            // Finally do the comparision
            if (this.isMinAggregation)
            {
                if (ItemComparer.Instance.Compare(unwrappedLocalMinMax, this.globalMinMax) < 0)
                {
                    this.globalMinMax = unwrappedLocalMinMax;
                }
            }
            else
            {
                if (ItemComparer.Instance.Compare(unwrappedLocalMinMax, this.globalMinMax) > 0)
                {
                    this.globalMinMax = unwrappedLocalMinMax;
                }
            }
        }

        public CosmosElement GetResult()
        {
            object result;
            if (this.globalMinMax == ItemComparer.MinValue || this.globalMinMax == ItemComparer.MaxValue)
            {
                // The filter did not match any documents.
                result = Undefined.Value;
            }
            else
            {
                result = this.globalMinMax;
            }

            CosmosElement cosmosElement;
            if(result == Undefined.Value)
            {
                cosmosElement = null;
            }
            else
            {
                cosmosElement = CosmosElement.FromObject(this.globalMinMax);
            }

            return cosmosElement;
        }
    }
}
