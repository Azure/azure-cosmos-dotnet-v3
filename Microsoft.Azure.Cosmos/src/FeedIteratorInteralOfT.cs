//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Internal API for FeedIterator<typeparamref name="T"/> for inheritance and mocking purposes.
    /// </summary>
#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName
    internal abstract class FeedIteratorInternal<T> : FeedIterator<T>
#pragma warning restore SA1649
    {
        public abstract bool TryGetContinuationToken(out string continuationToken);
    }
}