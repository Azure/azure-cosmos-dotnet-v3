// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;

    /// <summary>
    /// FeedRange that represents an effective partition key range.
    /// </summary>
    internal sealed class FeedRangeEpk : FeedRangeInternal
    {
        public static readonly FeedRangeEpk FullRange = new FeedRangeEpk(new Documents.Routing.Range<string>(
            Documents.Routing.PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
            Documents.Routing.PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
            isMinInclusive: true,
            isMaxInclusive: false));

        public FeedRangeEpk(Documents.Routing.Range<string> range)
        {
            this.Range = range ?? throw new ArgumentNullException(nameof(range));
        }

        public Documents.Routing.Range<string> Range { get; }

        internal override Task<List<Documents.Routing.Range<string>>> GetEffectiveRangesAsync(
            IRoutingMapProvider routingMapProvider,
            string containerRid,
            Documents.PartitionKeyDefinition partitionKeyDefinition)
        {
            return Task.FromResult(new List<Documents.Routing.Range<string>>() { this.Range });
        }

        internal override async Task<IEnumerable<string>> GetPartitionKeyRangesAsync(
            IRoutingMapProvider routingMapProvider,
            string containerRid,
            Documents.PartitionKeyDefinition partitionKeyDefinition,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<Documents.PartitionKeyRange> partitionKeyRanges = await routingMapProvider.TryGetOverlappingRangesAsync(
                containerRid, 
                this.Range,
                NoOpTrace.Singleton,
                forceRefresh: false);
            return partitionKeyRanges.Select(partitionKeyRange => partitionKeyRange.Id);
        }

        internal override void Accept(IFeedRangeVisitor visitor)
        {
            visitor.Visit(this);
        }

        internal override void Accept<TInput>(IFeedRangeVisitor<TInput> visitor, TInput input)
        {
            visitor.Visit(this, input);
        }

        internal override TOutput Accept<TInput, TOutput>(IFeedRangeVisitor<TInput, TOutput> visitor, TInput input)
        {
            return visitor.Visit(this, input);
        }

        internal override Task<TResult> AcceptAsync<TResult>(
            IFeedRangeAsyncVisitor<TResult> visitor,
            CancellationToken cancellationToken = default)
        {
            return visitor.VisitAsync(this, cancellationToken);
        }

        internal override Task<TResult> AcceptAsync<TResult, TArg>(
            IFeedRangeAsyncVisitor<TResult, TArg> visitor,
            TArg argument,
            CancellationToken cancellationToken) => visitor.VisitAsync(this, argument, cancellationToken);

        public override string ToString() => this.Range.ToString();

        internal override TResult Accept<TResult>(IFeedRangeTransformer<TResult> transformer)
        {
            return transformer.Visit(this);
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as FeedRangeEpk);
        }

        public bool Equals(FeedRangeEpk other)
        {
            return (other != null)
                && this.Range.Min.Equals(other.Range.Min)
                && this.Range.Max.Equals(other.Range.Max)
                && this.Range.IsMinInclusive.Equals(other.Range.IsMinInclusive)
                && this.Range.IsMaxInclusive.Equals(other.Range.IsMaxInclusive);
        }

        public override int GetHashCode()
        {
            return this.Range.Min.GetHashCode()
                ^ this.Range.Max.GetHashCode()
                ^ this.Range.IsMinInclusive.GetHashCode()
                ^ this.Range.IsMaxInclusive.GetHashCode();
        }
    }
}
