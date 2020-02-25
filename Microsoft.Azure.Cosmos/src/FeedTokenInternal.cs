// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;

    internal abstract class FeedTokenInternal : FeedToken
    {
        public string ContainerRid { get; }

        public FeedTokenInternal()
        {
        }

        public FeedTokenInternal(string containerRid)
        {
            this.ContainerRid = containerRid;
        }

        public abstract void EnrichRequest(RequestMessage request);

        public abstract string GetContinuation();

        public abstract void UpdateContinuation(string continuationToken);

        public abstract bool IsDone { get; }

        public abstract Task<List<Documents.Routing.Range<string>>> GetAffectedRangesAsync(
            IRoutingMapProvider routingMapProvider,
            string containerRid,
            Documents.PartitionKeyDefinition partitionKeyDefinition);

        public abstract Task<IEnumerable<string>> GetPartitionKeyRangesAsync(
            IRoutingMapProvider routingMapProvider,
            string containerRid,
            Documents.PartitionKeyDefinition partitionKeyDefinition,
            CancellationToken cancellationToken);

        public abstract void ValidateContainer(string containerRid);

        public static bool TryParse(
            string toStringValue,
            out FeedToken parsedToken)
        {
            if (FeedTokenEPKRange.TryParseInstance(toStringValue, out parsedToken))
            {
                return true;
            }

            if (FeedTokenPartitionKey.TryParseInstance(toStringValue, out parsedToken))
            {
                return true;
            }

            if (FeedTokenPartitionKeyRange.TryParseInstance(toStringValue, out parsedToken))
            {
                return true;
            }

            parsedToken = null;
            return false;
        }

        public virtual Task<bool> ShouldRetryAsync(
            ContainerCore containerCore,
            ResponseMessage responseMessage,
            CancellationToken cancellationToken = default(CancellationToken)) => Task.FromResult(false);

        public override IReadOnlyList<FeedToken> Scale() => new List<FeedToken>();
    }
}
