// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ReadFeed.Pagination
{
    using System;
    using Microsoft.Azure.Cosmos.CosmosElements;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif
        sealed class ReadFeedContinuationState : ReadFeedState 
    {
        public ReadFeedContinuationState(CosmosElement continuationToken)
        {
            this.ContinuationToken = continuationToken ?? throw new ArgumentNullException(nameof(continuationToken));
        }

        public CosmosElement ContinuationToken { get; }
    }
}
