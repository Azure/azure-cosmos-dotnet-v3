//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.QueryPlan
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.OrderBy;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Aggregate;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Distinct;
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
        public IReadOnlyList<SortOrder> OrderBy
        {
            get;
            set;
        }

        [JsonProperty("orderByExpressions")]
        public IReadOnlyList<string> OrderByExpressions
        {
            get;
            set;
        }

        [JsonProperty("groupByExpressions")]
        public IReadOnlyList<string> GroupByExpressions
        {
            get;
            set;
        }

        [JsonProperty("groupByAliases")]
        public IReadOnlyList<string> GroupByAliases
        {
            get;
            set;
        }

        [JsonProperty("aggregates", ItemConverterType = typeof(StringEnumConverter))]
        public IReadOnlyList<AggregateOperator> Aggregates
        {
            get;
            set;
        }

        [JsonProperty("groupByAliasToAggregateType", ItemConverterType = typeof(StringEnumConverter))]
        public IReadOnlyDictionary<string, AggregateOperator?> GroupByAliasToAggregateType
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

        public bool HasAggregates
        {
            get
            {
                bool aggregatesListNonEmpty = (this.Aggregates != null) && (this.Aggregates.Count > 0);
                if (aggregatesListNonEmpty)
                {
                    return true;
                }

                bool aggregateAliasMappingNonEmpty = (this.GroupByAliasToAggregateType != null)
                    && this.GroupByAliasToAggregateType
                        .Values
                        .Any(aggregateOperator => aggregateOperator.HasValue);
                return aggregateAliasMappingNonEmpty;
            }
        }

        public bool HasGroupBy
        {
            get
            {
                return this.GroupByExpressions != null && this.GroupByExpressions.Count > 0;
            }
        }

        public bool HasOrderBy
        {
            get
            {
                return this.OrderBy != null && this.OrderBy.Count > 0;
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
    }
}