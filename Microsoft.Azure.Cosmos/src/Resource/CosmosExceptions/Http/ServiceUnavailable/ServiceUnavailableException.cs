// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.ServiceUnavailable
{
    using System;
    using System.Net;

    internal abstract class ServiceUnavailableException : CosmosHttpWithSubstatusCodeException
    {
        protected ServiceUnavailableException(int subStatusCode)
            : this(subStatusCode, message: null)
        {
        }

        protected ServiceUnavailableException(int subStatusCode, string message)
            : this(subStatusCode, message: message, innerException: null)
        {
        }

        protected ServiceUnavailableException(int subStatusCode, string message, Exception innerException)
            : base(statusCode: HttpStatusCode.ServiceUnavailable, subStatusCode: subStatusCode, message: message, innerException: innerException)
        {
        }
    }
}
