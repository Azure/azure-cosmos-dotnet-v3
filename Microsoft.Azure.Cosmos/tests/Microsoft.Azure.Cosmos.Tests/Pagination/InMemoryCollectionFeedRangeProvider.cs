namespace Microsoft.Azure.Cosmos.Tests.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Pagination;

    internal sealed class InMemoryCollectionFeedRangeProvider : IFeedRangeProvider
    {
        private readonly InMemoryCollection inMemoryCollection;

        public InMemoryCollectionFeedRangeProvider(InMemoryCollection inMemoryCollection)
        {
            this.inMemoryCollection = inMemoryCollection ?? throw new ArgumentNullException(nameof(inMemoryCollection));
        }

        public Task<IEnumerable<FeedRange>> GetChildRangeAsync(
            FeedRange feedRange,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!(feedRange is FeedRangePartitionKeyRange feedRangePartitionKeyRange))
            {
                throw new ArgumentOutOfRangeException(nameof(feedRange));
            }

            int partitionKeyRangeId = int.Parse(feedRangePartitionKeyRange.PartitionKeyRangeId);
            (int leftChild, int rightChild) = this.inMemoryCollection.GetChildRanges(partitionKeyRangeId);

            return Task.FromResult(
                (IEnumerable<FeedRange>)new List<FeedRange>()
                {
                    new FeedRangePartitionKeyRange(leftChild.ToString()),
                    new FeedRangePartitionKeyRange(rightChild.ToString()),
                });
        }

        public Task<IEnumerable<FeedRange>> GetFeedRangesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            List<FeedRange> ranges = new List<FeedRange>();
            foreach (int partitionKeyRangeId in this.inMemoryCollection.PartitionKeyRangeFeedReed().Keys)
            {
                FeedRange range = new FeedRangePartitionKeyRange(partitionKeyRangeId.ToString());
                ranges.Add(range);
            }

            return Task.FromResult((IEnumerable<FeedRange>)ranges);
        }
    }
}
