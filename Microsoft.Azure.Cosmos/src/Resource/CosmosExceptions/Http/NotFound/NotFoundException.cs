// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.NotFound
{
    using System;
    using System.Net;

    internal abstract class NotFoundException : CosmosHttpException
    {
        protected NotFoundException(int subStatusCode)
            : this(subStatusCode, message: null)
        {
        }

        protected NotFoundException(int subStatusCode, string message)
            : this(subStatusCode, message: message, innerException: null)
        {
        }

        protected NotFoundException(int subStatusCode, string message, Exception innerException)
            : base(statusCode: HttpStatusCode.NotFound, subStatusCode: subStatusCode, message: message, innerException: innerException)
        {
        }
    }
}
