// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;

    internal sealed class MalformedChangeFeedContinuationTokenException : ChangeFeedException
    {
        public MalformedChangeFeedContinuationTokenException()
            : base()
        {
        }

        public MalformedChangeFeedContinuationTokenException(string message)
            : base(message)
        {
        }

        public MalformedChangeFeedContinuationTokenException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
