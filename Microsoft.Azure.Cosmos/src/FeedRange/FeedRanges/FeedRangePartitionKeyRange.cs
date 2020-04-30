// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;

    /// <summary>
    /// FeedRange that represents a Partition Key Range.
    /// Backward compatibility implementation to transition from V2 SDK queries that were filtering by PKRangeId.
    /// </summary>
    internal sealed class FeedRangePartitionKeyRange : FeedRangeInternal
    {
        public string PartitionKeyRangeId { get; }

        public FeedRangePartitionKeyRange(string partitionKeyRangeId)
        {
            this.PartitionKeyRangeId = partitionKeyRangeId;
        }

        public override async Task<List<Documents.Routing.Range<string>>> GetEffectiveRangesAsync(
            IRoutingMapProvider routingMapProvider,
            string containerRid,
            Documents.PartitionKeyDefinition partitionKeyDefinition)
        {
            Documents.PartitionKeyRange pkRange = await routingMapProvider.TryGetPartitionKeyRangeByIdAsync(
                collectionResourceId: containerRid,
                partitionKeyRangeId: this.PartitionKeyRangeId,
                forceRefresh: true);

            if (pkRange == null)
            {
                throw new InvalidOperationException();
            }

            return new List<Documents.Routing.Range<string>> { pkRange.ToRange() };
        }

        public override Task<IEnumerable<string>> GetPartitionKeyRangesAsync(
            IRoutingMapProvider routingMapProvider,
            string containerRid,
            Documents.PartitionKeyDefinition partitionKeyDefinition,
            CancellationToken cancellationToken)
        {
            IEnumerable<string> partitionKeyRanges = new List<string>() { this.PartitionKeyRangeId };
            return Task.FromResult(partitionKeyRanges);
        }

        public override void Accept(FeedRangeVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override string ToString() => this.PartitionKeyRangeId;
    }
}
