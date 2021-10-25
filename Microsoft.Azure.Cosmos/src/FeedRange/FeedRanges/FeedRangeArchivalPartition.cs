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
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    internal class FeedRangeArchivalPartition : FeedRangeInternal
    {
        public FeedRangeArchivalPartition(string dataPKRangeId, SplitGraph splitGraph)
        {
            this.DataRangeId = dataPKRangeId;
            this.SplitGraph = splitGraph;
        }

        public string DataRangeId { get; }

        public FeedRangeEpk EpkRange
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// PKRangeId of leaf partition that owns the archival PKRange.
        /// </summary>
        public string RoutingPartitionKeyRangeId
        {
            get { throw new NotImplementedException(); }
        }

        public SplitGraph SplitGraph { get; }

        internal override Task<List<Documents.Routing.Range<string>>> GetEffectiveRangesAsync(
            IRoutingMapProvider routingMapProvider,
            string containerRid,
            Documents.PartitionKeyDefinition partitionKeyDefinition,
            ITrace trace) => new FeedRangePartitionKeyRange(this.DataRangeId).GetEffectiveRangesAsync(
                routingMapProvider, containerRid, partitionKeyDefinition, trace);

        internal override Task<IEnumerable<string>> GetPartitionKeyRangesAsync(
            IRoutingMapProvider routingMapProvider,
            string containerRid,
            Documents.PartitionKeyDefinition partitionKeyDefinition,
            CancellationToken cancellationToken,
            ITrace trace) => new FeedRangePartitionKeyRange(this.DataRangeId).GetPartitionKeyRangesAsync(
                    routingMapProvider,
                    containerRid,
                    partitionKeyDefinition,
                    cancellationToken,
                    trace);

        internal override void Accept(IFeedRangeVisitor visitor)
        {
            visitor.Visit(this);
        }

        internal override void Accept<TInput>(IFeedRangeVisitor<TInput> visitor, TInput input) =>
            throw new NotSupportedException();

        internal override TOutput Accept<TInput, TOutput>(IFeedRangeVisitor<TInput, TOutput> visitor, TInput input) =>
            throw new NotSupportedException();

        internal override Task<TResult> AcceptAsync<TResult>(
            IFeedRangeAsyncVisitor<TResult> visitor,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        internal override Task<TResult> AcceptAsync<TResult, TArg>(
           IFeedRangeAsyncVisitor<TResult, TArg> visitor,
           TArg argument,
           CancellationToken cancellationToken) => throw new NotSupportedException();

        public override string ToString() => this.RoutingPartitionKeyRangeId;

        internal override TResult Accept<TResult>(IFeedRangeTransformer<TResult> transformer) => throw new NotSupportedException();
    }
}
