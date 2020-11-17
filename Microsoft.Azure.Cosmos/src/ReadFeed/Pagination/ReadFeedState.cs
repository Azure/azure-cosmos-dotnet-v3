// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ReadFeed.Pagination
{
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;

    internal abstract class ReadFeedState : State
    {
        public static ReadFeedState Beginning() => ReadFeedBeginningState.Singleton;

        public static ReadFeedState Continuation(CosmosElement cosmosElement) => new ReadFeedContinuationState(cosmosElement);
    }
}
