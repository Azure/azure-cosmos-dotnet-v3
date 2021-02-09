// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ReadFeed.Pagination
{
    using System;
    using Microsoft.Azure.Cosmos.CosmosElements;

    internal sealed class ReadFeedContinuationState : ReadFeedState 
    {
        public ReadFeedContinuationState(CosmosElement continuationToken)
        {
            this.ContinuationToken = continuationToken ?? throw new ArgumentNullException(nameof(continuationToken));
        }

        public CosmosElement ContinuationToken { get; }
    }
}
