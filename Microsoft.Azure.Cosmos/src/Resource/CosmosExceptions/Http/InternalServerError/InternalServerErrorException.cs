// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.InternalServerError
{
    using System;
    using System.Net;

    internal abstract class InternalServerErrorException : CosmosHttpWithSubstatusCodeException
    {
        protected InternalServerErrorException(int subStatusCode)
            : this(subStatusCode, message: null)
        {
        }

        protected InternalServerErrorException(int subStatusCode, string message)
            : this(subStatusCode, message: message, innerException: null)
        {
        }

        protected InternalServerErrorException(int subStatusCode, string message, Exception innerException)
            : base(statusCode: HttpStatusCode.InternalServerError, subStatusCode: subStatusCode, message: message, innerException: innerException)
        {
        }
    }
}
