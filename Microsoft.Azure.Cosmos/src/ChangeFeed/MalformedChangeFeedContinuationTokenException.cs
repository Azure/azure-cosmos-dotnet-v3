// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;
    using Microsoft.Azure.Cosmos.Tracing;

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

        public override TResult Accept<TResult>(ChangeFeedExceptionVisitor<TResult> visitor, ITrace trace)
        {
            return visitor.Visit(this, trace);
        }
    }
}
