// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.RequestTimeout
{
    using System;
    using System.Net;

    internal abstract class RequestTimeoutException : CosmosHttpWithSubstatusCodeException
    {
        protected RequestTimeoutException(int subStatusCode)
            : this(subStatusCode, message: null)
        {
        }

        protected RequestTimeoutException(int subStatusCode, string message)
            : this(subStatusCode, message: message, innerException: null)
        {
        }

        protected RequestTimeoutException(int subStatusCode, string message, Exception innerException)
            : base(statusCode: HttpStatusCode.RequestTimeout, subStatusCode: subStatusCode, message: message, innerException: innerException)
        {
        }
    }
}
