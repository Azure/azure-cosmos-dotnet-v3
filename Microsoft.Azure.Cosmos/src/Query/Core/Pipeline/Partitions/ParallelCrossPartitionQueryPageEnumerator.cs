// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Partitions
{
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Pagination;

    internal static class ParallelCrossPartitionQueryPageEnumerator
    {
        public static CrossPartitionRangePageEnumerator<QueryPage, QueryState> Create(
            IFeedRangeProvider feedRangeProvider,
            IQueryDataSource queryDataSource,
            SqlQuerySpec sqlQuerySpec,
            int pageSize,
            CrossPartitionState<QueryState> state = default)
        {
            return new CrossPartitionRangePageEnumerator<QueryPage, QueryState>(
                feedRangeProvider,
                ParallelCrossPartitionQueryPageEnumerator.MakeCreateFunction(queryDataSource, sqlQuerySpec, pageSize),
                Comparer.Singleton,
                state: state);
        }

        private static CreatePartitionRangePageEnumerator<QueryPage, QueryState> MakeCreateFunction(
            IQueryDataSource queryDataSource,
            SqlQuerySpec sqlQuerySpec,
            int pageSize) => (FeedRange range, QueryState state) => new QueryPartitionRangePageEnumerator(
                queryDataSource,
                sqlQuerySpec,
                range,
                pageSize,
                state);

        private sealed class Comparer : IComparer<PartitionRangePageEnumerator<QueryPage, QueryState>>
        {
            public static readonly Comparer Singleton = new Comparer();

            public int Compare(
                PartitionRangePageEnumerator<QueryPage, QueryState> partitionRangePageEnumerator1,
                PartitionRangePageEnumerator<QueryPage, QueryState> partitionRangePageEnumerator2)
            {
                if (object.ReferenceEquals(partitionRangePageEnumerator1, partitionRangePageEnumerator2))
                {
                    return 0;
                }

                if (partitionRangePageEnumerator1.HasMoreResults && !partitionRangePageEnumerator2.HasMoreResults)
                {
                    return -1;
                }

                if (!partitionRangePageEnumerator1.HasMoreResults && partitionRangePageEnumerator2.HasMoreResults)
                {
                    return 1;
                }

                // Either both don't have results or both do.
                return string.CompareOrdinal(
                    ((FeedRangePartitionKeyRange)partitionRangePageEnumerator1.Range).PartitionKeyRangeId,
                    ((FeedRangePartitionKeyRange)partitionRangePageEnumerator2.Range).PartitionKeyRangeId);
            }
        }
    }
}