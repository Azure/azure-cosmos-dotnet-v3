// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
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
            if (string.IsNullOrEmpty(containerRid))
            {
                throw new ArgumentNullException(nameof(containerRid));
            }

            this.ContainerRid = containerRid;
        }

        public abstract void FillHeaders(
            CosmosClientContext cosmosClientContext,
            RequestMessage request);

        public abstract string GetContinuation();

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
    }
}
