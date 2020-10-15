// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;

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

        public override Task<List<Documents.Routing.Range<string>>> GetEffectiveRangesAsync(
            IRoutingMapProvider routingMapProvider,
            string containerRid,
            Documents.PartitionKeyDefinition partitionKeyDefinition)
        {
            return Task.FromResult(
                new List<Documents.Routing.Range<string>>
                {
                    Documents.Routing.Range<string>.GetPointRange(
                        this.PartitionKey.InternalKey.GetEffectivePartitionKeyString(partitionKeyDefinition))
                });
        }

        public override async Task<IEnumerable<string>> GetPartitionKeyRangesAsync(
            IRoutingMapProvider routingMapProvider,
            string containerRid,
            Documents.PartitionKeyDefinition partitionKeyDefinition,
            CancellationToken cancellationToken)
        {
            string effectivePartitionKeyString = this.PartitionKey.InternalKey.GetEffectivePartitionKeyString(partitionKeyDefinition);
            Documents.PartitionKeyRange range = await routingMapProvider.TryGetRangeByEffectivePartitionKeyAsync(containerRid, effectivePartitionKeyString);
            return new List<string>() { range.Id };
        }

        public override void Accept(IFeedRangeVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override Task<TResult> AcceptAsync<TResult>(
            IFeedRangeAsyncVisitor<TResult> visitor,
            CancellationToken cancellationToken = default)
        {
            return visitor.VisitAsync(this, cancellationToken);
        }

        public override Task<TResult> AcceptAsync<TResult, TArg>(
           IFeedRangeAsyncVisitor<TResult, TArg> visitor,
           TArg argument,
           CancellationToken cancellationToken) => visitor.VisitAsync(this, argument, cancellationToken);

        public override string ToString() => this.PartitionKey.InternalKey.ToJsonString();
    }
}
