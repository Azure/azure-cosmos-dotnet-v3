// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.Conflict
{
    using System;
    using System.Net;

    internal abstract class ConflictException : CosmosHttpException
    {
        protected ConflictException(int subStatusCode)
            : this(subStatusCode, message: null)
        {
        }

        protected ConflictException(int subStatusCode, string message)
            : this(subStatusCode, message: message, innerException: null)
        {
        }

        protected ConflictException(int subStatusCode, string message, Exception innerException)
            : base(statusCode: HttpStatusCode.Conflict, subStatusCode: subStatusCode, message: message, innerException: innerException)
        {
        }
    }
}
