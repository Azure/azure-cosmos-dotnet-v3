//-----------------------------------------------------------------------
// <copyright file="MinMaxAggregator.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Aggregation
{
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json.Linq;

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

        public void Aggregate(object item)
        {
            // If the value became undefinded at some point then it should stay that way.
            if (this.globalMinMax == Undefined.Value)
            {
                return;
            }

            if (item == Undefined.Value)
            {
                // If we got an undefined in the pipeline then the whole thing becomes undefined.
                this.globalMinMax = Undefined.Value;
                return;
            }

            // Check to see if we got the higher precision result 
            // and unwrap the object to get the actual item of interest
            JObject jObject = item as JObject;
            if (jObject != null)
            {
                JToken countToken = jObject["count"];
                if (countToken != null)
                {
                    // We know the object looks like: {"min": MIN(c.blah), "count": COUNT(c.blah)}
                    if (countToken.ToObject<long>() == 0)
                    {
                        // Ignore the value since the continuation / partition had no results that matched the filter so min is undefined.
                        return;
                    }

                    JToken min = jObject["min"];
                    JToken max = jObject["max"];

                    // Note that JToken won't equal null as long as a value is there
                    // even if that value is a JSON null.
                    if (min != null)
                    {
                        item = min.ToObject<object>();
                    }
                    else if (max != null)
                    {
                        item = max.ToObject<object>();
                    }
                    else
                    {
                        item = Undefined.Value;
                    }
                }
            }

            if (!ItemComparer.IsMinOrMax(this.globalMinMax) 
                && (!ItemTypeHelper.IsPrimitive(item) || !ItemTypeHelper.IsPrimitive(this.globalMinMax)))
            {
                // This means we are comparing non primitives with is undefined
                this.globalMinMax = Undefined.Value;
                return;
            }

            // Finally do the comparision
            if (this.isMinAggregation)
            {
                if (ItemComparer.Instance.Compare(item, this.globalMinMax) < 0)
                {
                    this.globalMinMax = item;
                }
            }
            else
            {
                if (ItemComparer.Instance.Compare(item, this.globalMinMax) > 0)
                {
                    this.globalMinMax = item;
                }
            }
        }

        public object GetResult()
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

            return result;
        }
    }
}
