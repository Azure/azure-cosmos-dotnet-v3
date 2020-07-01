// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.Parallel
{
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.ItemProducers;

    internal sealed class ParallelCrossPartitionQueryPageEnumerator : CrossPartitionRangePageEnumerator<QueryPage, QueryState>
    {
        public ParallelCrossPartitionQueryPageEnumerator(
            IFeedRangeProvider feedRangeProvider,
            IQueryDataSource queryDataSource,
            SqlQuerySpec sqlQuerySpec,
            int pageSize,
            CrossPartitionState<QueryState> state = default)
            : base(
                  feedRangeProvider: feedRangeProvider,
                  createPartitionRangeEnumerator: ParallelCrossPartitionQueryPageEnumerator.MakeCreateFunction(queryDataSource, sqlQuerySpec, pageSize),
                  comparer: Comparer.Singleton,
                  state: state)
        {
        }

        public static CreatePartitionRangePageEnumerator<QueryPage, QueryState> MakeCreateFunction(
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
