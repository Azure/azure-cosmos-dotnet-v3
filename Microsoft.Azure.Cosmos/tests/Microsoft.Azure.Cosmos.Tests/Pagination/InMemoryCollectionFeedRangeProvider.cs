namespace Microsoft.Azure.Cosmos.Tests.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Documents;

    internal sealed class InMemoryCollectionFeedRangeProvider : IFeedRangeProvider
    {
        private readonly InMemoryCollection inMemoryCollection;

        public InMemoryCollectionFeedRangeProvider(InMemoryCollection inMemoryCollection)
        {
            this.inMemoryCollection = inMemoryCollection ?? throw new ArgumentNullException(nameof(inMemoryCollection));
        }

        public Task<IEnumerable<PartitionKeyRange>> GetChildRangeAsync(
            PartitionKeyRange feedRange,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int partitionKeyRangeId = int.Parse(feedRange.Id);
            (int leftChild, int rightChild) = this.inMemoryCollection.GetChildRanges(partitionKeyRangeId);

            return Task.FromResult(
                (IEnumerable<PartitionKeyRange>)new List<PartitionKeyRange>()
                {
                    new PartitionKeyRange()
                    {
                        Id = leftChild.ToString(),
                        MinInclusive = leftChild.ToString(),
                        MaxExclusive = leftChild.ToString(),
                    },
                    new PartitionKeyRange()
                    {
                        Id = rightChild.ToString(),
                        MinInclusive = rightChild.ToString(),
                        MaxExclusive = rightChild.ToString(),
                    }
                });
        }

        public Task<IEnumerable<PartitionKeyRange>> GetFeedRangesAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            List<PartitionKeyRange> ranges = new List<PartitionKeyRange>();
            foreach (int partitionKeyRangeId in this.inMemoryCollection.PartitionKeyRangeFeedReed().Keys)
            {
                PartitionKeyRange range = new PartitionKeyRange()
                {
                    Id = partitionKeyRangeId.ToString(),
                    MinInclusive = partitionKeyRangeId.ToString(),
                    MaxExclusive = partitionKeyRangeId.ToString(),
                };

                ranges.Add(range);
            }

            return Task.FromResult((IEnumerable<PartitionKeyRange>)ranges);
        }
    }
}
