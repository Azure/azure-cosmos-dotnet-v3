namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionContext
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;

    internal static class QueryRequestOptionsTuner
    {
        private static readonly IReadOnlyDictionary<QueryMetadata, int> OptimalMaxPageSizeLookup;
        private static readonly IReadOnlyDictionary<QueryMetadata, int> OptimalMaxBufferedItemCountSizeLookup;
        private static readonly IReadOnlyDictionary<QueryMetadata, int> OptimalMaxConcurrencyLookup;

        public static int GetOptimalMaxPageSize(PartitionedQueryExecutionInfo partitionedQueryExecutionInfo)
        {
            if (partitionedQueryExecutionInfo == null)
            {
                throw new ArgumentNullException(nameof(partitionedQueryExecutionInfo));
            }

            QueryMetadata queryMetadata = QueryMetadata.Create(partitionedQueryExecutionInfo);
            return QueryRequestOptionsTuner.OptimalMaxPageSizeLookup[queryMetadata];
        }

        public static int GetOptimalMaxBufferedItemCount(PartitionedQueryExecutionInfo partitionedQueryExecutionInfo)
        {
            if (partitionedQueryExecutionInfo == null)
            {
                throw new ArgumentNullException(nameof(partitionedQueryExecutionInfo));
            }

            QueryMetadata queryMetadata = QueryMetadata.Create(partitionedQueryExecutionInfo);
            return QueryRequestOptionsTuner.OptimalMaxBufferedItemCountSizeLookup[queryMetadata];
        }

        public static int GetOptimalMaxConcurrency(PartitionedQueryExecutionInfo partitionedQueryExecutionInfo)
        {
            if (partitionedQueryExecutionInfo == null)
            {
                throw new ArgumentNullException(nameof(partitionedQueryExecutionInfo));
            }

            QueryMetadata queryMetadata = QueryMetadata.Create(partitionedQueryExecutionInfo);
            return QueryRequestOptionsTuner.OptimalMaxConcurrencyLookup[queryMetadata];
        }

        private readonly struct QueryMetadata
        {
            public QueryMetadata(bool hasAggregate, bool hasDistinct, bool hasGroupBy, bool hasOrderBy, bool hasOffsetLimit)
            {
                this.HasAggregate = hasAggregate;
                this.HasDistinct = hasDistinct;
                this.HasGroupBy = hasGroupBy;
                this.HasOrderBy = hasOrderBy;
                this.HasOffsetLimit = hasOffsetLimit;
            }

            public bool HasAggregate { get; }
            public bool HasDistinct { get; }
            public bool HasGroupBy { get; }
            public bool HasOrderBy { get; }
            public bool HasOffsetLimit { get; }

            public override bool Equals(object obj)
            {
                if (!(obj is QueryMetadata queryMetadata))
                {
                    return false;
                }

                return this.Equals(queryMetadata);
            }

            public bool Equals(QueryMetadata other)
            {
                return this.HasAggregate == other.HasAggregate &&
                    this.HasDistinct == other.HasDistinct &&
                    this.HasGroupBy == other.HasGroupBy &&
                    this.HasOffsetLimit == other.HasOffsetLimit &&
                    this.HasOrderBy == other.HasOrderBy;
            }

            public override int GetHashCode()
            {
                return ((this.HasAggregate ? 1 : 0) << 5) +
                    ((this.HasDistinct ? 1 : 0) << 4) +
                    ((this.HasGroupBy ? 1 : 0) << 3) +
                    ((this.HasOffsetLimit ? 1 : 0) << 2) +
                    ((this.HasOrderBy ? 1 : 0) << 1);
            }

            public static QueryMetadata Create(PartitionedQueryExecutionInfo partitionedQueryExecutionInfo)
            {
                return new QueryMetadata(
                    hasAggregate: partitionedQueryExecutionInfo.QueryInfo.HasAggregates,
                    hasDistinct: partitionedQueryExecutionInfo.QueryInfo.HasDistinct,
                    hasGroupBy: partitionedQueryExecutionInfo.QueryInfo.HasGroupBy,
                    hasOrderBy: partitionedQueryExecutionInfo.QueryInfo.HasOrderBy,
                    hasOffsetLimit: partitionedQueryExecutionInfo.QueryInfo.HasOffset || partitionedQueryExecutionInfo.QueryInfo.HasLimit);
            }
        }
    }
}
