// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Monads;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;

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
            TryCatch<Documents.PartitionKeyRange> tryGetPartitionKeyRangeByIdAsync = await routingMapProvider.TryGetPartitionKeyRangeByIdAsync(
                collectionResourceId: containerRid,
                partitionKeyRangeId: this.PartitionKeyRangeId,
                forceRefresh: false);

            if (tryGetPartitionKeyRangeByIdAsync.Failed)
            {
                // Try with a refresh
                tryGetPartitionKeyRangeByIdAsync = await routingMapProvider.TryGetPartitionKeyRangeByIdAsync(
                    collectionResourceId: containerRid,
                    partitionKeyRangeId: this.PartitionKeyRangeId,
                    forceRefresh: true);
            }

            if (tryGetPartitionKeyRangeByIdAsync.Failed)
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
                    innerException: tryGetPartitionKeyRangeByIdAsync.Exception);
            }

            return new List<Documents.Routing.Range<string>> { tryGetPartitionKeyRangeByIdAsync.Result.ToRange() };
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
