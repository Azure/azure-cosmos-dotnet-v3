// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Monads;
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
            return Task.FromResult(new List<Documents.Routing.Range<string>>
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
            TryCatch<Documents.PartitionKeyRange> tryGetRangeByEffectivePartitionKeyAsync = await routingMapProvider.TryGetRangeByEffectivePartitionKeyAsync(
                containerRid,
                effectivePartitionKeyString);
            tryGetRangeByEffectivePartitionKeyAsync.ThrowIfFailed();

            return new List<string>() { tryGetRangeByEffectivePartitionKeyAsync.Result.Id };
        }

        public override void Accept(FeedRangeVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override string ToString() => this.PartitionKey.InternalKey.ToJsonString();
    }
}
