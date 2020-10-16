//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Cosmos.CosmosElements;

    /// <summary>
    /// Internal feed iterator API for casting and mocking purposes.
    /// </summary>
#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    abstract class FeedIteratorInternal : FeedIterator
    {
        public abstract CosmosElement GetCosmosElementContinuationToken();

        public static bool IsRetriableException(CosmosException cosmosException)
        {
            return ((int)cosmosException.StatusCode == 429)
                || (cosmosException.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
                || (cosmosException.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable);
        }
    }
}