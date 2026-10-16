//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if PREVIEW
#nullable enable
namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Represents captured feed range membership and boundaries for partition key ownership lookups.
    /// </summary>
    /// <remarks>
    /// Create a snapshot using <see cref="Container.CreateFeedRangeSnapshot"/>.
    /// The snapshot can be used with handles for the same database and container obtained from the same client.
    /// Changes to the source list or range representations after creation do not change the captured boundaries or membership.
    /// Lookups return the original range objects; the snapshot does not make those objects immutable.
    /// </remarks>
    public sealed class FeedRangeSnapshot
    {
        internal FeedRangeSnapshot(ContainerCore container, ContainerCore.FeedRangeEpkIndex index)
        {
            this.ClientContext = container.ClientContext;
            this.DatabaseId = container.Database.Id;
            this.ContainerId = container.Id;
            this.Index = index;
        }

        internal CosmosClientContext ClientContext { get; }

        internal string DatabaseId { get; }

        internal string ContainerId { get; }

        internal ContainerCore.FeedRangeEpkIndex Index { get; }
    }
}
#endif
