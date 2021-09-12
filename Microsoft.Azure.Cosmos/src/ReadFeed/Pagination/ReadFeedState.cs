// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ReadFeed.Pagination
{
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;

    #if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif 
        abstract class ReadFeedState : State
    {
        public static ReadFeedState Beginning() => ReadFeedBeginningState.Singleton;

        public static ReadFeedState Continuation(CosmosElement cosmosElement) => new ReadFeedContinuationState(cosmosElement);
    }
}
