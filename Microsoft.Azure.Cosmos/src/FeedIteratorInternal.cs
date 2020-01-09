//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Internal feed iterator API for casting and mocking purposes.
    /// </summary>
    internal abstract class FeedIteratorInternal : FeedIterator
    {
        public abstract bool TryGetContinuationToken(out string continuationToken);
    }
}