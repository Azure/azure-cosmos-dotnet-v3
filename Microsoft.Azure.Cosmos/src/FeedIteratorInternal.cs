//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Cosmos.Json;

    /// <summary>
    /// Internal feed iterator API for casting and mocking purposes.
    /// </summary>
    internal abstract class FeedIteratorInternal : FeedIterator
    {
        public abstract bool TryGetContinuationToken(out string continuationToken);
        public abstract void SerializeState(IJsonWriter jsonWriter);
    }
}