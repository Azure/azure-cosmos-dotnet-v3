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

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif
        sealed class FeedRangeEpkRange : FeedRangeInternal
    {
        public static readonly FeedRangeEpkRange FullRange = new FeedRangeEpkRange(
            Documents.Routing.PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
            Documents.Routing.PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey);

        public FeedRangeEpkRange(string startEpkInclusive, string endEpkExclusive)
        {
            this.StartEpkInclusive = startEpkInclusive ?? throw new ArgumentNullException(nameof(startEpkInclusive));
            this.EndEpkExclusive = endEpkExclusive ?? throw new ArgumentNullException(nameof(endEpkExclusive));
        }

        public string StartEpkInclusive { get; }

        public string EndEpkExclusive { get; }

        internal Documents.Routing.Range<string> Range => new Documents.Routing.Range<string>(this.StartEpkInclusive, this.EndEpkExclusive, isMinInclusive: true, isMaxInclusive: false);

        internal override Task<List<Documents.Routing.Range<string>>> GetEffectiveRangesAsync(
            IRoutingMapProvider routingMapProvider,
            string containerRid,
            Documents.PartitionKeyDefinition partitionKeyDefinition)
        {
            return Task.FromResult(
                new List<Documents.Routing.Range<string>>() 
                { 
                    new Documents.Routing.Range<string>(this.StartEpkInclusive, this.EndEpkExclusive, isMinInclusive: true, isMaxInclusive: false) 
                });
        }

        internal override async Task<IEnumerable<string>> GetPartitionKeyRangesAsync(
            IRoutingMapProvider routingMapProvider,
            string containerRid,
            Documents.PartitionKeyDefinition partitionKeyDefinition,
            CancellationToken cancellationToken)
        {
            Documents.Routing.Range<string> range = new Documents.Routing.Range<string>(
                this.StartEpkInclusive, 
                this.EndEpkExclusive, 
                isMinInclusive: true, 
                isMaxInclusive: false);
            IReadOnlyList<Documents.PartitionKeyRange> partitionKeyRanges = await routingMapProvider.TryGetOverlappingRangesAsync(
                containerRid,
                range,
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

        public override string ToString() => new Documents.Routing.Range<string>(this.StartEpkInclusive, this.EndEpkExclusive, isMinInclusive: true, isMaxInclusive: false).ToString();

        internal override TResult Accept<TResult>(IFeedRangeTransformer<TResult> transformer)
        {
            return transformer.Visit(this);
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as FeedRangeEpkRange);
        }

        public bool Equals(FeedRangeEpkRange other)
        {
            return (other != null)
                && this.StartEpkInclusive.Equals(other.StartEpkInclusive)
                && this.EndEpkExclusive.Equals(other.EndEpkExclusive);
        }

        public override int GetHashCode()
        {
            return this.StartEpkInclusive.GetHashCode()
                ^ this.EndEpkExclusive.GetHashCode();
        }
    }
}
