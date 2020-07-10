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
        private readonly FeedRangeToEffectivePartitionKeyRangeVisitor feedRangeToEffectivePartitionKeyRangeVisitor;
        private readonly FeedRangeToPhysicalPartitionKeyRange feedRangeToPhysicalPartitionKeyRange;

        public InMemoryCollectionFeedRangeProvider(InMemoryCollection inMemoryCollection)
        {
            this.inMemoryCollection = inMemoryCollection ?? throw new ArgumentNullException(nameof(inMemoryCollection));
            this.feedRangeToEffectivePartitionKeyRangeVisitor = new FeedRangeToEffectivePartitionKeyRangeVisitor(this.inMemoryCollection);
            this.feedRangeToPhysicalPartitionKeyRange = new FeedRangeToPhysicalPartitionKeyRange(this.inMemoryCollection);
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

        public Task<FeedRangeEpk> ToEffectivePartitionKeyRangeAsync(
            FeedRangeInternal feedRange,
            CancellationToken cancellationToken) => feedRange.AcceptAsync(this.feedRangeToEffectivePartitionKeyRangeVisitor);

        public Task<FeedRangePartitionKeyRange> ToPhysicalPartitionKeyRangeAsync(
            FeedRangeInternal feedRange,
            CancellationToken cancellationToken) => feedRange.AcceptAsync(this.feedRangeToPhysicalPartitionKeyRange);

        private sealed class FeedRangeToEffectivePartitionKeyRangeVisitor : IFeedRangeAsyncVisitor<FeedRangeEpk>
        {
            private readonly InMemoryCollection inMemoryCollection;

            public FeedRangeToEffectivePartitionKeyRangeVisitor(InMemoryCollection inMemoryCollection)
            {
                this.inMemoryCollection = inMemoryCollection ?? throw new ArgumentNullException(nameof(inMemoryCollection));
            }

            public Task<FeedRangeEpk> VisitAsync(FeedRangePartitionKey feedRange, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<FeedRangeEpk> VisitAsync(FeedRangePartitionKeyRange feedRange, CancellationToken cancellationToken = default)
            {
                PartitionKeyHashRange hashRange = this.inMemoryCollection.GetHashRange(int.Parse(feedRange.PartitionKeyRangeId));
                Documents.Routing.Range<string> range = new Documents.Routing.Range<string>(
                    min: hashRange.StartInclusive.HasValue ? hashRange.StartInclusive.Value.ToString() : string.Empty,
                    max: hashRange.EndExclusive.HasValue ? hashRange.EndExclusive.Value.ToString() : string.Empty,
                    isMinInclusive: true,
                    isMaxInclusive: false);

                return Task.FromResult(new FeedRangeEpk(range));
            }

            public Task<FeedRangeEpk> VisitAsync(FeedRangeEpk feedRange, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(feedRange);
            }
        }

        private sealed class FeedRangeToPhysicalPartitionKeyRange : IFeedRangeAsyncVisitor<FeedRangePartitionKeyRange>
        {
            private readonly InMemoryCollection inMemoryCollection;

            public FeedRangeToPhysicalPartitionKeyRange(InMemoryCollection inMemoryCollection)
            {
                this.inMemoryCollection = inMemoryCollection ?? throw new ArgumentNullException(nameof(inMemoryCollection));
            }

            public Task<FeedRangePartitionKeyRange> VisitAsync(FeedRangePartitionKey feedRange, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<FeedRangePartitionKeyRange> VisitAsync(FeedRangePartitionKeyRange feedRange, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(feedRange);
            }

            public Task<FeedRangePartitionKeyRange> VisitAsync(FeedRangeEpk feedRange, CancellationToken cancellationToken = default)
            {
                Documents.Routing.Range<string> range = feedRange.Range;

                PartitionKeyHash? startInclusive;
                if (range.Min == string.Empty)
                {
                    startInclusive = default;
                }
                else
                {
                    if (!PartitionKeyHash.TryParse(range.Min, out PartitionKeyHash parsedValue))
                    {
                        throw new InvalidOperationException($"Failed to parse: {range.Min}.");
                    }

                    startInclusive = parsedValue;
                }

                PartitionKeyHash? endExclusive;
                if (range.Max == string.Empty)
                {
                    endExclusive = default;
                }
                else
                {
                    if (!PartitionKeyHash.TryParse(range.Max, out PartitionKeyHash parsedValue))
                    {
                        throw new InvalidOperationException($"Failed to parse: {range.Max}.");
                    }

                    endExclusive = parsedValue;
                }

                int pkRangeId = this.inMemoryCollection.GetPartitionKeyRangeId(new PartitionKeyHashRange(startInclusive, endExclusive));
                return Task.FromResult(new FeedRangePartitionKeyRange(pkRangeId.ToString()));
            }
        }
    }
}
