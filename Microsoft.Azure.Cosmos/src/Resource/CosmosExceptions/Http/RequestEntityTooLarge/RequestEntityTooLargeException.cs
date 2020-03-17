// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.RequestEntityTooLarge
{
    using System;
    using System.Net;

    internal abstract class RequestEntityTooLargeException : CosmosHttpWithSubstatusCodeException
    {
        protected RequestEntityTooLargeException(int subStatusCode)
            : this(subStatusCode, message: null)
        {
        }

        protected RequestEntityTooLargeException(int subStatusCode, string message)
            : this(subStatusCode, message: message, innerException: null)
        {
        }

        protected RequestEntityTooLargeException(int subStatusCode, string message, Exception innerException)
            : base(statusCode: HttpStatusCode.RequestEntityTooLarge, subStatusCode: subStatusCode, message: message, innerException: innerException)
        {
        }
    }
}
