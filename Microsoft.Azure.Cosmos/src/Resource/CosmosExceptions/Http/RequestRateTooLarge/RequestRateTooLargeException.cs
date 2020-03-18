// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.RequestRateTooLarge
{
    using System;
    using System.Net;

    internal abstract class RequestRateTooLargeException : CosmosHttpWithSubstatusCodeException
    {
        protected RequestRateTooLargeException(TimeSpan retryAfter, int subStatusCode)
            : this(retryAfter: retryAfter, subStatusCode, message: null)
        {
        }

        protected RequestRateTooLargeException(TimeSpan retryAfter, int subStatusCode, string message)
            : this(retryAfter: retryAfter, subStatusCode, message: message, innerException: null)
        {
        }

        protected RequestRateTooLargeException(TimeSpan retryAfter, int subStatusCode, string message, Exception innerException)
            : base(statusCode: (HttpStatusCode)429, retryAfter: retryAfter, subStatusCode: subStatusCode, message: message, innerException: innerException)
        {
        }
    }
}