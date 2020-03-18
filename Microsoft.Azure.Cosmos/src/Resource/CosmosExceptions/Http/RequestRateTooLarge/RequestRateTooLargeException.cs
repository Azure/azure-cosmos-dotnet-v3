// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.RequestRateTooLarge
{
    using System;
    using System.Net;

    internal abstract class RequestRateTooLargeException : CosmosException
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
#pragma warning disable CS0618 // Type or member is obsolete
            : base(
                (HttpStatusCode)429,
                message,
                subStatusCode,
                stackTrace: null,
                activityId: null,
                requestCharge: 0,
                retryAfter,
                headers: null,
                diagnosticsContext: null,
                error: null,
                innerException)
#pragma warning restore CS0618 // Type or member is obsolete
        {
        }
    }
}