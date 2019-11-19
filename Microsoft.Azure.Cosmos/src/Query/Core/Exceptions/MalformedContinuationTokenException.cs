// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Exceptions
{
    using System;

    internal sealed class MalformedContinuationTokenException : QueryException
    {
        public MalformedContinuationTokenException()
            : base()
        {
        }

        public MalformedContinuationTokenException(string message)
            : base(message)
        {
        }

        public MalformedContinuationTokenException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public override TResult Accept<TResult>(QueryExceptionVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }
    }
}
