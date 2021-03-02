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
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif
        sealed class FeedRangePhysicalPartitionKeyRange : FeedRangeInternal
    {
        public FeedRangePhysicalPartitionKeyRange(string partitionKeyRangeId)
        {
            this.PartitionKeyRangeId = partitionKeyRangeId ?? throw new ArgumentNullException(nameof(partitionKeyRangeId));
        }

        public string PartitionKeyRangeId { get; }

        internal override async Task<List<Documents.Routing.Range<string>>> GetEffectiveRangesAsync(
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
                    message: $"The PartitionKeyRangeId: \"{this.PartitionKeyRangeId}\" is not valid for the current container {containerRid} .",
                    stackTrace: string.Empty,
                    headers: new Headers()
                    {
                        SubStatusCode = SubStatusCodes.PartitionKeyRangeGone,
                    },
                    error: null,
                    innerException: null,
                    trace: NoOpTrace.Singleton);
            }

            return new List<Documents.Routing.Range<string>> { pkRange.ToRange() };
        }

        internal override Task<IEnumerable<string>> GetPartitionKeyRangesAsync(
            IRoutingMapProvider routingMapProvider,
            string containerRid,
            Documents.PartitionKeyDefinition partitionKeyDefinition,
            CancellationToken cancellationToken)
        {
            IEnumerable<string> partitionKeyRanges = new List<string>() { this.PartitionKeyRangeId };
            return Task.FromResult(partitionKeyRanges);
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

        public override string ToString() => this.PartitionKeyRangeId;

        internal override TResult Accept<TResult>(IFeedRangeTransformer<TResult> transformer)
        {
            return transformer.Visit(this);
        }
    }
}
