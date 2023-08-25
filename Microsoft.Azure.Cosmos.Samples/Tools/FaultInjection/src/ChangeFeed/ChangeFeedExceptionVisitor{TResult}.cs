// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using Microsoft.Azure.Cosmos.Tracing;

    internal abstract class ChangeFeedExceptionVisitor<TResult>
    {
        internal abstract TResult Visit(MalformedChangeFeedContinuationTokenException malformedChangeFeedContinuationTokenException, ITrace trace);
    }
}
