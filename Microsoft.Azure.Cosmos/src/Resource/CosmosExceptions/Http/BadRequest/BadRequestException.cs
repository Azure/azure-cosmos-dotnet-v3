// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.BadRequest
{
    using System;
    using System.Net;

    internal abstract class BadRequestException : CosmosHttpWithSubstatusCodeException
    {
        protected BadRequestException(int subStatusCode)
            : this(subStatusCode, message: null)
        {
        }

        protected BadRequestException(int subStatusCode, string message)
            : this(subStatusCode, message: message, innerException: null)
        {
        }

        protected BadRequestException(int subStatusCode, string message, Exception innerException)
            : base(statusCode: HttpStatusCode.BadRequest, subStatusCode: subStatusCode, message: message, innerException: innerException)
        {
        }
    }
}
