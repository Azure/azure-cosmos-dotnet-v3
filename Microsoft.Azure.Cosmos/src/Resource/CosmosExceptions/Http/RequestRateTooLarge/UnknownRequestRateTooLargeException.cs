// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.RequestRateTooLarge
{
    using System;

    internal sealed class UnknownRequestRateTooLargeException : RequestRateTooLargeException
    {
        public UnknownRequestRateTooLargeException(TimeSpan retryAfter, int subStatusCode)
            : this(retryAfter: retryAfter, subStatusCode: subStatusCode, message: null)
        {
        }

        public UnknownRequestRateTooLargeException(TimeSpan retryAfter, int subStatusCode, string message)
            : this(retryAfter: retryAfter, subStatusCode: subStatusCode, message: message, innerException: null)
        {
        }

        public UnknownRequestRateTooLargeException(TimeSpan retryAfter, int subStatusCode, string message, Exception innerException)
            : base(retryAfter, subStatusCode: subStatusCode, message: message, innerException: innerException)
        {
        }
    }
}
