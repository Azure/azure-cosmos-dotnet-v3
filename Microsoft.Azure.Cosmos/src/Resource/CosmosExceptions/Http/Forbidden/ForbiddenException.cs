// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.Forbidden
{
    using System;
    using System.Net;

    internal abstract class ForbiddenException : CosmosHttpException
    {
        protected ForbiddenException(int subStatusCode)
            : this(subStatusCode, message: null)
        {
        }

        protected ForbiddenException(int subStatusCode, string message)
            : this(subStatusCode, message: message, innerException: null)
        {
        }

        protected ForbiddenException(int subStatusCode, string message, Exception innerException)
            : base(statusCode: HttpStatusCode.Forbidden, subStatusCode: subStatusCode, message: message, innerException: innerException)
        {
        }
    }
}
