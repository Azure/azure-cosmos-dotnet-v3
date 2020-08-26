// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// FeedRange that represents a Partition Key Range.
    /// Backward compatibility implementation to transition from V2 SDK queries that were filtering by PKRangeId.
    /// </summary>
    internal sealed class FeedRangePartitionKeyRange : FeedRangeInternal
    {
        public FeedRangePartitionKeyRange(string partitionKeyRangeId)
        {
            this.PartitionKeyRangeId = partitionKeyRangeId;
        }

        public string PartitionKeyRangeId { get; }

        public override async Task<List<Documents.Routing.Range<string>>> GetEffectiveRangesAsync(
            IRoutingMapProvider routingMapProvider,
            string containerRid,
            Documents.PartitionKeyDefinition partitionKeyDefinition)
        {
            Documents.PartitionKeyRange pkRange = await routingMapProvider.TryGetPartitionKeyRangeByIdAsync(
                collectionResourceId: containerRid,
                partitionKeyRangeId: this.PartitionKeyRangeId,
                forceRefresh: false);

            if (pkRange == null)
            {
                // Try with a refresh
                pkRange = await routingMapProvider.TryGetPartitionKeyRangeByIdAsync(
                    collectionResourceId: containerRid,
                    partitionKeyRangeId: this.PartitionKeyRangeId,
                    forceRefresh: true);
            }

            if (pkRange == null)
            {
                throw CosmosExceptionFactory.Create(
                    statusCode: HttpStatusCode.Gone,
                    subStatusCode: (int)SubStatusCodes.PartitionKeyRangeGone,
                    message: $"The PartitionKeyRangeId: \"{this.PartitionKeyRangeId}\" is not valid for the current container {containerRid} .",
                    stackTrace: string.Empty,
                    activityId: string.Empty,
                    requestCharge: 0,
                    retryAfter: null,
                    headers: null,
                    diagnosticsContext: null,
                    error: null,
                    innerException: null);
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

        public override void Accept(IFeedRangeVisitor visitor) => visitor.Visit(this);

        public override Task<TResult> AcceptAsync<TResult>(
            IFeedRangeAsyncVisitor<TResult> visitor,
            CancellationToken cancellationToken = default) => visitor.VisitAsync(this, cancellationToken);

        public override string ToString() => this.PartitionKeyRangeId;
    }
}
