namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.ItemProducers
{
    using System;
    using Microsoft.Azure.Cosmos.Pagination;

    internal abstract class QueryPartitionRangePageEnumerator : PartitionRangePageEnumerator
    {
        protected readonly SqlQuerySpec sqlQuerySpec;

        public QueryPartitionRangePageEnumerator(
            SqlQuerySpec sqlQuerySpec,
            FeedRange feedRange,
            State state = default)
            : base(feedRange, state)
        {
            this.sqlQuerySpec = sqlQuerySpec ?? throw new ArgumentNullException(nameof(sqlQuerySpec));
        }
    }
}
