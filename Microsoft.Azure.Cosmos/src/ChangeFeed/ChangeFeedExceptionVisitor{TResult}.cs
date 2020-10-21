// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    internal abstract class ChangeFeedExceptionVisitor<TResult>
    {
        internal abstract TResult Visit(MalformedChangeFeedContinuationTokenException malformedChangeFeedContinuationTokenException);
    }
}
