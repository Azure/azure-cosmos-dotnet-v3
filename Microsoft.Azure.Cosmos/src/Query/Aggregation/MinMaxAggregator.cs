using Microsoft.Azure.Cosmos.Internal;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.Cosmos.Query.Aggregation
{
    internal sealed class MinMaxAggregator : IAggregator
    {
        private readonly bool isMinAggregation;
        private object value;
        private bool initialized;

        public MinMaxAggregator(bool isMinAggregation)
        {
            this.isMinAggregation = isMinAggregation;
        }

        public void Aggregate(object item)
        {
            // If the value became undefinded at some point then it should stay that way.
            if (this.value == Undefined.Value)
            {
                return;
            }

            if (item == Undefined.Value)
            {
                // If we got an undefined in the pipeline then the whole thing becomes undefined.
                this.value = Undefined.Value;
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

            if (!this.initialized)
            {
                // If this is the first item then just take it
                this.value = item;
                this.initialized = true;
                return;
            }

            if (!ItemTypeHelper.IsPrimitive(item) || !ItemTypeHelper.IsPrimitive(this.value))
            {
                // This means we are comparing non primitives with is undefined
                this.value = Undefined.Value;
                return;
            }

            if (this.isMinAggregation)
            {
                if (ItemComparer.Instance.Compare(item, this.value) < 0)
                {
                    this.value = item;
                }
            }
            else
            {
                if (ItemComparer.Instance.Compare(item, this.value) > 0)
                {
                    this.value = item;
                }
            }

            this.initialized = true;
        }

        public object GetResult()
        {
            return this.initialized ? this.value : Undefined.Value;
        }
    }
}
