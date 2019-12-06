//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Internal API for FeedIterator<typeparamref name="T"/> for inheritance and mocking purposes.
    /// </summary>
#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName
#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif
    abstract class FeedIteratorInternal<T> : FeedIterator<T>
#pragma warning restore SA1649
    {
        public abstract bool TryGetContinuationToken(out string continuationToken);
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}