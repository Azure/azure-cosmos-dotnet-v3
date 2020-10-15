// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ReadFeed.Pagination
{
    using System;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;

    internal sealed class ReadFeedState : State
    {
        public ReadFeedState(string continuationToken)
        {
            this.ContinuationToken = continuationToken ?? throw new ArgumentNullException(nameof(continuationToken));
        }

        public string ContinuationToken { get; }
    }
}
