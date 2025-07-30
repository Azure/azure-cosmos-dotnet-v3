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

    public sealed class QueryInfo
    {
        [System.Text.Json.Serialization.JsonPropertyName("distinctType")]
        [System.Text.Json.Serialization.JsonConverter(typeof(JsonStringEnumConverter<DistinctQueryType>))]
        public DistinctQueryType DistinctType
        {
            get;
            set;
        }

        [System.Text.Json.Serialization.JsonPropertyName("top")]
        public uint? Top
        {
            get;
            set;
        }

        [System.Text.Json.Serialization.JsonPropertyName("offset")]
        public uint? Offset
        {
            get;
            set;
        }

        [System.Text.Json.Serialization.JsonPropertyName("limit")]
        public uint? Limit
        {
            get;
            set;
        }

        [System.Text.Json.Serialization.JsonPropertyName("orderBy")]
        [System.Text.Json.Serialization.JsonConverter(typeof(Microsoft.Azure.Cosmos.stj.JsonStringEnumListConverter<SortOrder>))]
        public IReadOnlyList<SortOrder> OrderBy
        {
            get;
            set;
        }

        [System.Text.Json.Serialization.JsonPropertyName("orderByExpressions")]
        public IReadOnlyList<string> OrderByExpressions
        {
            get;
            set;
        }

        [System.Text.Json.Serialization.JsonPropertyName("groupByExpressions")]
        public IReadOnlyList<string> GroupByExpressions
        {
            get;
            set;
        }

        [System.Text.Json.Serialization.JsonPropertyName("groupByAliases")]
        public IReadOnlyList<string> GroupByAliases
        {
            get;
            set;
        }

        [System.Text.Json.Serialization.JsonPropertyName("aggregates")]
        [System.Text.Json.Serialization.JsonConverter(typeof(Microsoft.Azure.Cosmos.stj.JsonStringEnumListConverter<AggregateOperator>))]
        public IReadOnlyList<AggregateOperator> Aggregates
        {
            get;
            set;
        }

        [System.Text.Json.Serialization.JsonPropertyName("groupByAliasToAggregateType")]
        [System.Text.Json.Serialization.JsonConverter(typeof(Microsoft.Azure.Cosmos.stj.JsonStringEnumNullableDictionaryConverter<AggregateOperator>))]
        public IReadOnlyDictionary<string, AggregateOperator?> GroupByAliasToAggregateType
        {
            get;
            set;
        }

        [System.Text.Json.Serialization.JsonPropertyName("rewrittenQuery")]
        public string RewrittenQuery
        {
            get;
            set;
        }

        [System.Text.Json.Serialization.JsonPropertyName("hasSelectValue")]
        public bool HasSelectValue
        {
            get;
            set;
        }

        [System.Text.Json.Serialization.JsonPropertyName("dCountInfo")]
        public DCountInfo DCountInfo
        {
            get;
            set;
        }

        [System.Text.Json.Serialization.JsonPropertyName("hasNonStreamingOrderBy")]
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