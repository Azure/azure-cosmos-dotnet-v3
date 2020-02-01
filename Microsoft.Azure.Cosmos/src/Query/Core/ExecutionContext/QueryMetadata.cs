// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionContext
{
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    readonly struct QueryMetadata
    {
        public enum TShirtSize
        {
            XSmall,
            Small,
            Medium,
            Large,
            XLarge
        }

        public QueryMetadata(
            bool hasAggregate,
            bool hasDistinct,
            bool hasGroupBy,
            bool hasOrderBy,
            int? offsetCount,
            int? limitCount,
            int? topCount)
        {
            this.HasAggregate = hasAggregate;
            this.HasDistinct = hasDistinct;
            this.HasGroupBy = hasGroupBy;
            this.HasOrderBy = hasOrderBy;
            this.OffsetCount = offsetCount;
            this.LimitCount = limitCount;
            this.TopCount = topCount;
        }

        public bool HasAggregate { get; }
        public bool HasDistinct { get; }
        public bool HasGroupBy { get; }
        public bool HasOrderBy { get; }
        public int? OffsetCount { get; }
        public int? LimitCount { get; }
        public int? TopCount { get; }

        public bool HasOffset => this.OffsetCount.HasValue;
        public bool HasLimit => this.LimitCount.HasValue;
        public bool HasTop => this.TopCount.HasValue;
        public bool HasOffsetLimitOrTop => this.HasOffset || this.HasLimit || this.HasTop;
        public int? OffsetLimitTopCount => this.HasOffsetLimitOrTop ? (int?)(this.OffsetCount.GetValueOrDefault(0) + this.LimitCount.GetValueOrDefault(0) + this.TopCount.GetValueOrDefault(0)) : null;

        public TShirtSize? OffsetTShirtSize => QueryMetadata.GetTShirtSize(this.OffsetCount);
        public TShirtSize? LimitTShirtSize => QueryMetadata.GetTShirtSize(this.LimitCount);
        public TShirtSize? TopTShirtSize => QueryMetadata.GetTShirtSize(this.TopCount);
        public TShirtSize? OffsetLimitTopTShirtSize => QueryMetadata.GetTShirtSize(this.OffsetLimitTopCount);

        private static TShirtSize? GetTShirtSize(int? size)
        {
            if (!size.HasValue)
            {
                return default;
            }

            int value = size.Value;
            TShirtSize tShirtSize;
            if (value <= 1)
            {
                tShirtSize = TShirtSize.XSmall;
            }
            else if (value <= 10)
            {
                tShirtSize = TShirtSize.Small;
            }
            else if (value <= 128)
            {
                tShirtSize = TShirtSize.Medium;
            }
            else if (value <= 1024)
            {
                tShirtSize = TShirtSize.Large;
            }
            else
            {
                tShirtSize = TShirtSize.XLarge;
            }

            return tShirtSize;
        }

        /// <summary>
        /// Determines if a query has partial results, meaning the SDK has to drain the query fully before returning a single result.
        /// </summary>
        public bool HasPartialResults => this.HasAggregate || this.HasDistinct || this.HasGroupBy;

        internal static QueryMetadata Create(PartitionedQueryExecutionInfo partitionedQueryExecutionInfo)
        {
            return new QueryMetadata(
                hasAggregate: partitionedQueryExecutionInfo.QueryInfo.HasAggregates,
                hasDistinct: partitionedQueryExecutionInfo.QueryInfo.HasDistinct,
                hasGroupBy: partitionedQueryExecutionInfo.QueryInfo.HasGroupBy,
                hasOrderBy: partitionedQueryExecutionInfo.QueryInfo.HasOrderBy,
                offsetCount: partitionedQueryExecutionInfo.QueryInfo.Offset,
                limitCount: partitionedQueryExecutionInfo.QueryInfo.Limit,
                topCount: partitionedQueryExecutionInfo.QueryInfo.Top);
        }
    }
}
