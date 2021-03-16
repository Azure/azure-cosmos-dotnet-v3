// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;

    /// <summary>
    /// FeedRange that represents an exact Partition Key value.
    /// </summary>
    internal sealed class FeedRangePartitionKey : FeedRangeInternal
    {
        public PartitionKey PartitionKey { get; }

        public FeedRangePartitionKey(PartitionKey partitionKey)
        {
            this.PartitionKey = partitionKey;
        }

        internal override Task<List<Documents.Routing.Range<string>>> GetEffectiveRangesAsync(
            IRoutingMapProvider routingMapProvider,
            string containerRid,
            Documents.PartitionKeyDefinition partitionKeyDefinition,
            ITrace trace)
        {
            return Task.FromResult(
                new List<Documents.Routing.Range<string>>
                {
                    Documents.Routing.Range<string>.GetPointRange(
                        this.PartitionKey.InternalKey.GetEffectivePartitionKeyString(partitionKeyDefinition))
                });
        }

        internal override async Task<IEnumerable<string>> GetPartitionKeyRangesAsync(
            IRoutingMapProvider routingMapProvider,
            string containerRid,
            Documents.PartitionKeyDefinition partitionKeyDefinition,
            CancellationToken cancellationToken,
            ITrace trace)
        {
            string effectivePartitionKeyString = this.PartitionKey.InternalKey.GetEffectivePartitionKeyString(partitionKeyDefinition);
            Documents.PartitionKeyRange range = await routingMapProvider.TryGetRangeByEffectivePartitionKeyAsync(containerRid, effectivePartitionKeyString, trace);
            return new List<string>() { range.Id };
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

        public override string ToString() => this.PartitionKey.InternalKey.ToJsonString();

        internal override TResult Accept<TResult>(IFeedRangeTransformer<TResult> transformer)
        {
            return transformer.Visit(this);
        }
    }
}
