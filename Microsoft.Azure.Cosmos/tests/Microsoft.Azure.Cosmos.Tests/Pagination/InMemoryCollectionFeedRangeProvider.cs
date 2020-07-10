namespace Microsoft.Azure.Cosmos.Tests.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Routing;
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
            PartitionKeyHashRange leftChildHashRange = this.inMemoryCollection.GetHashRange(leftChild);
            PartitionKeyHashRange rightChildHashRange = this.inMemoryCollection.GetHashRange(rightChild);

            return Task.FromResult(
                (IEnumerable<PartitionKeyRange>)new List<PartitionKeyRange>()
                {
                    new PartitionKeyRange()
                    {
                        Id = leftChild.ToString(),
                        MinInclusive = leftChildHashRange.StartInclusive.HasValue ? leftChildHashRange.StartInclusive.Value.ToString() : string.Empty,
                        MaxExclusive = leftChildHashRange.EndExclusive.HasValue ? leftChildHashRange.EndExclusive.Value.ToString() : string.Empty,
                    },
                    new PartitionKeyRange()
                    {
                        Id = rightChild.ToString(),
                        MinInclusive = rightChildHashRange.StartInclusive.HasValue ? rightChildHashRange.StartInclusive.Value.ToString() : string.Empty,
                        MaxExclusive = rightChildHashRange.EndExclusive.HasValue ? rightChildHashRange.EndExclusive.Value.ToString() : string.Empty,
                    }
                });
        }

        public Task<IEnumerable<PartitionKeyRange>> GetFeedRangesAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            List<PartitionKeyRange> ranges = new List<PartitionKeyRange>();
            foreach (int partitionKeyRangeId in this.inMemoryCollection.PartitionKeyRangeFeedReed().Keys)
            {
                PartitionKeyHashRange hashRange = this.inMemoryCollection.GetHashRange(partitionKeyRangeId);
                PartitionKeyRange range = new PartitionKeyRange()
                {
                    Id = partitionKeyRangeId.ToString(),
                    MinInclusive = hashRange.StartInclusive.HasValue ? hashRange.StartInclusive.Value.ToString() : string.Empty,
                    MaxExclusive = hashRange.EndExclusive.HasValue ? hashRange.EndExclusive.Value.ToString() : string.Empty,
                };

                ranges.Add(range);
            }

            return Task.FromResult((IEnumerable<PartitionKeyRange>)ranges);
        }
    }
}
