//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Tracing;

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
        public static bool IsRetriableException(CosmosException cosmosException)
        {
            return ((int)cosmosException.StatusCode == 429)
                || (cosmosException.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
                || (cosmosException.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable);
        }

        public abstract Task<ResponseMessage> ReadNextAsync(ITrace trace, CancellationToken cancellationToken);
    }
}