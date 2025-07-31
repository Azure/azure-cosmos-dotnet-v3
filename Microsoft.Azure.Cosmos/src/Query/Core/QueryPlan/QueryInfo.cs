//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.QueryPlan
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Aggregate;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Distinct;

    internal sealed class QueryInfo
    {
        [JsonPropertyName("distinctType")]
        public DistinctQueryType DistinctType
        {
            get;
            set;
        }

        [JsonPropertyName("top")]
        public uint? Top
        {
            get;
            set;
        }

        [JsonPropertyName("offset")]
        public uint? Offset
        {
            get;
            set;
        }

        [JsonPropertyName("limit")]
        public uint? Limit
        {
            get;
            set;
        }

        [JsonPropertyName("orderBy")]
        public IReadOnlyList<SortOrder> OrderBy
        {
            get;
            set;
        }

        [JsonPropertyName("orderByExpressions")]
        public IReadOnlyList<string> OrderByExpressions
        {
            get;
            set;
        }

        [JsonPropertyName("groupByExpressions")]
        public IReadOnlyList<string> GroupByExpressions
        {
            get;
            set;
        }

        [JsonPropertyName("groupByAliases")]
        public IReadOnlyList<string> GroupByAliases
        {
            get;
            set;
        }

        [JsonPropertyName("aggregates")]
        public IReadOnlyList<AggregateOperator> Aggregates
        {
            get;
            set;
        }

        [JsonPropertyName("groupByAliasToAggregateType")]
        public IReadOnlyDictionary<string, AggregateOperator?> GroupByAliasToAggregateType
        {
            get;
            set;
        }

        [JsonPropertyName("rewrittenQuery")]
        public string RewrittenQuery
        {
            get;
            set;
        }

        [JsonPropertyName("hasSelectValue")]
        public bool HasSelectValue
        {
            get;
            set;
        }

        [JsonPropertyName("dCountInfo")]
        public DCountInfo DCountInfo
        {
            get;
            set;
        }

        [JsonPropertyName("hasNonStreamingOrderBy")]
        public bool HasNonStreamingOrderBy
        {
            get;
            set;
        }

        public bool HasDCount => this.DCountInfo != null;

        public bool HasDistinct => this.DistinctType != DistinctQueryType.None;
        public bool HasTop => this.Top.HasValue;

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

        public bool HasGroupBy => (this.GroupByExpressions != null) && (this.GroupByExpressions.Count > 0);

        public bool HasOrderBy => (this.OrderBy != null) && (this.OrderBy.Count > 0);

        public bool HasOffset => this.Offset.HasValue;

        public bool HasLimit => this.Limit.HasValue;
    }
}