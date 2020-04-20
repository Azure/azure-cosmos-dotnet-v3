// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

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
            ContainerInternal containerCore,
            ResponseMessage responseMessage,
            CancellationToken cancellationToken = default(CancellationToken)) => Task.FromResult(false);

        public override IReadOnlyList<FeedToken> Scale() => new List<FeedToken>();
    }
}
