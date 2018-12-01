//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Aggregation
{
    using System;
    using Microsoft.Azure.Cosmos.Internal;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal sealed class AverageAggregator : IAggregator
    {
        private AverageInfo info;

        public void Aggregate(dynamic item)
        {
            AverageInfo newInfo = ((JObject) item).ToObject<AverageInfo>();
            if (this.info == null)
            {
                this.info = newInfo;
            }
            else
            {
                this.info += newInfo;
            }
        }

        public object GetResult()
        {
            return this.info == null ? Undefined.Value : this.info.GetAverage();
        }

        private sealed class AverageInfo
        {
            public AverageInfo(double? sum, long count)
            {
                this.Sum = sum;
                this.Count = count;
            }

            [JsonProperty("sum")]
            public double? Sum
            {
                get;
                private set;
            }

            [JsonProperty("count")]
            public long Count
            {
                get;
                private set;
            }

            public static AverageInfo operator +(AverageInfo info1, AverageInfo info2)
            {
                if (info1 == null || info2 == null)
                {
                    return null;
                }

                if (!info1.Sum.HasValue || !info2.Sum.HasValue)
                {
                    return new AverageInfo(null, info1.Count + info2.Count);
                }

                return new AverageInfo(info1.Sum + info2.Sum, info1.Count + info2.Count);
            }

            public object GetAverage()
            {
                if (!this.Sum.HasValue || this.Count <= 0)
                {
                    return Undefined.Value;
                }

                return this.Sum / this.Count;
            }
        }
    }
}