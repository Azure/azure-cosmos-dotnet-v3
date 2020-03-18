// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.RequestRateTooLarge
{
    using System;

    internal sealed class DefaultRequestRateTooLargeException : RequestRateTooLargeException
    {
        public DefaultRequestRateTooLargeException(TimeSpan retryAfter)
            : this(retryAfter: retryAfter, message: null)
        {
        }

        public DefaultRequestRateTooLargeException(TimeSpan retryAfter, string message)
            : this(retryAfter: retryAfter, message: message, innerException: null)
        {
        }

        public DefaultRequestRateTooLargeException(TimeSpan retryAfter, string message, Exception innerException)
            : base(retryAfter, subStatusCode: 0, message: message, innerException: innerException)
        {
        }
    }
}
