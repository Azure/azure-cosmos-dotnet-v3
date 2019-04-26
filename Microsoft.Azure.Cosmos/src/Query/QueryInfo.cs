//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class QueryInfo
    {
        [JsonProperty("distinctType")]
        [JsonConverter(typeof(StringEnumConverter))]
        public DistinctQueryType DistinctType
        {
            get;
            set;
        }

        [JsonProperty("top")]
        public int? Top
        {
            get;
            set;
        }

        [JsonProperty("offset")]
        public int? Offset
        {
            get;
            set;
        }

        [JsonProperty("limit")]
        public int? Limit
        {
            get;
            set;
        }

        [JsonProperty("orderBy", ItemConverterType = typeof(StringEnumConverter))]
        public SortOrder[] OrderBy
        {
            get;
            set;
        }

        [JsonProperty("orderByExpressions")]
        public string[] OrderByExpressions
        {
            get;
            set;
        }

        [JsonProperty("aggregates", ItemConverterType = typeof(StringEnumConverter))]
        public AggregateOperator[] Aggregates
        {
            get;
            set;
        }

        [JsonProperty("rewrittenQuery")]
        public string RewrittenQuery
        {
            get;
            set;
        }

        [JsonProperty("hasSelectValue")]
        public bool HasSelectValue
        {
            get;
            set;
        }

        public bool HasDistinct
        {
            get
            {
                return this.DistinctType != DistinctQueryType.None;
            }
        }
        public bool HasTop
        {
            get
            {
                return this.Top != null;
            }
        }

        public bool HasOffset
        {
            get
            {
                return this.Offset != null;
            }
        }

        public bool HasLimit
        {
            get
            {
                return this.Limit != null;
            }
        }

        public bool HasOrderBy
        {
            get
            {
                return this.OrderBy != null && this.OrderBy.Length > 0;
            }
        }

        public bool HasAggregates
        {
            get
            {
                return this.Aggregates != null && this.Aggregates.Length > 0;
            }
        }
    }
}
