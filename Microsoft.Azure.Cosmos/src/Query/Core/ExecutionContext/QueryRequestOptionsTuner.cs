// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionContext
{
    using System;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;

    internal static class QueryRequestOptionsTuner
    {
        private static readonly TShirtSizes ConcurrencyTShirtSizes = new TShirtSizes(
            xsmall: 0,
            small: 1,
            medium: 4,
            large: 16,
            xlarge: int.MaxValue);

        private static readonly TShirtSizes BufferedItemCountTShirtSizes = new TShirtSizes(
            xsmall: 0,
            small: 1024,
            medium: 4 * 1024,
            large: 16 * 1024,
            xlarge: int.MaxValue);

        private static readonly TShirtSizes PageSizeTShirtSizes = new TShirtSizes(
            xsmall: 100,
            small: 512,
            medium: 1024,
            large: 4 * 1024,
            xlarge: int.MaxValue);

        public static int GetOptimalMaxPageSize(PartitionedQueryExecutionInfo partitionedQueryExecutionInfo)
        {
            return PageSizeTShirtSizes.XLarge;
        }

        public static int GetOptimalMaxBufferedItemCount(PartitionedQueryExecutionInfo partitionedQueryExecutionInfo)
        {
            if (partitionedQueryExecutionInfo == null)
            {
                throw new ArgumentNullException(nameof(partitionedQueryExecutionInfo));
            }

            QueryMetadata queryMetadata = QueryMetadata.Create(partitionedQueryExecutionInfo);
            if (queryMetadata.HasPartialResults)
            {
                // If we are going to wait for the query to fully drain we might as well do it as fast as possible.
                return BufferedItemCountTShirtSizes.XLarge;
            }

            if (queryMetadata.HasOrderBy)
            {
                // Since we need to block the query until we see all the results we might as well get them as fast as possible.
                return BufferedItemCountTShirtSizes.Medium;
            }

            if (!queryMetadata.OffsetLimitTopTShirtSize.HasValue)
            {
                return BufferedItemCountTShirtSizes.XSmall;
            }

            int bufferedItemCount;
            switch (queryMetadata.OffsetLimitTopTShirtSize.Value)
            {
                case QueryMetadata.TShirtSize.XSmall:
                    bufferedItemCount = BufferedItemCountTShirtSizes.XSmall;
                    break;

                case QueryMetadata.TShirtSize.Small:
                    bufferedItemCount = BufferedItemCountTShirtSizes.Small;
                    break;

                case QueryMetadata.TShirtSize.Medium:
                    bufferedItemCount = BufferedItemCountTShirtSizes.Medium;
                    break;

                case QueryMetadata.TShirtSize.Large:
                    bufferedItemCount = BufferedItemCountTShirtSizes.Large;
                    break;

                case QueryMetadata.TShirtSize.XLarge:
                    bufferedItemCount = BufferedItemCountTShirtSizes.XLarge;
                    break;

                default:
                    throw new ArgumentOutOfRangeException($"Unknown {nameof(QueryMetadata.TShirtSize)}: {queryMetadata.OffsetLimitTopTShirtSize.Value}.");
            }

            return bufferedItemCount;
        }

        public static int GetOptimalMaxConcurrency(PartitionedQueryExecutionInfo partitionedQueryExecutionInfo)
        {
            if (partitionedQueryExecutionInfo == null)
            {
                throw new ArgumentNullException(nameof(partitionedQueryExecutionInfo));
            }

            QueryMetadata queryMetadata = QueryMetadata.Create(partitionedQueryExecutionInfo);
            if (queryMetadata.HasPartialResults)
            {
                // If we are going to wait for the query to fully drain we might as well do it as fast as possible.
                return ConcurrencyTShirtSizes.XLarge;
            }

            if (queryMetadata.HasOrderBy)
            {
                // Since we need to block the query until we see all the results we might as well get them as fast as possible.
                return ConcurrencyTShirtSizes.XLarge;
            }

            if (!queryMetadata.OffsetLimitTopTShirtSize.HasValue)
            {
                return ConcurrencyTShirtSizes.XSmall;
            }

            int concurrency;
            switch (queryMetadata.OffsetLimitTopTShirtSize.Value)
            {
                case QueryMetadata.TShirtSize.XSmall:
                    concurrency = ConcurrencyTShirtSizes.XSmall;
                    break;

                case QueryMetadata.TShirtSize.Small:
                    concurrency = ConcurrencyTShirtSizes.Small;
                    break;

                case QueryMetadata.TShirtSize.Medium:
                    concurrency = ConcurrencyTShirtSizes.Medium;
                    break;

                case QueryMetadata.TShirtSize.Large:
                    concurrency = ConcurrencyTShirtSizes.Large;
                    break;

                case QueryMetadata.TShirtSize.XLarge:
                    concurrency = ConcurrencyTShirtSizes.XLarge;
                    break;

                default:
                    throw new ArgumentOutOfRangeException($"Unknown {nameof(QueryMetadata.TShirtSize)}: {queryMetadata.OffsetLimitTopTShirtSize.Value}.");
            }

            return concurrency;
        }

        private readonly struct TShirtSizes
        {
            public TShirtSizes(int xsmall, int small, int medium, int large, int xlarge)
            {
                this.XSmall = xsmall >= 0 ? xsmall : throw new ArgumentOutOfRangeException(nameof(xsmall));
                this.Small = small >= 0 ? small : throw new ArgumentOutOfRangeException(nameof(small));
                this.Medium = medium >= 0 ? medium : throw new ArgumentOutOfRangeException(nameof(medium));
                this.Large = large >= 0 ? large : throw new ArgumentOutOfRangeException(nameof(large));
                this.XLarge = xlarge >= 0 ? xlarge : throw new ArgumentOutOfRangeException(nameof(xlarge));
            }

            public int XSmall { get; }
            public int Small { get; }
            public int Medium { get; }
            public int Large { get; }
            public int XLarge { get; }
        }

        private readonly ref struct QueryMetadata
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

            public static QueryMetadata Create(PartitionedQueryExecutionInfo partitionedQueryExecutionInfo)
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
}
