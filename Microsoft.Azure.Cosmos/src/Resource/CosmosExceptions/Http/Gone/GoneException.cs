// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.Gone
{
    using System;
    using System.Net;

    internal abstract class GoneException : CosmosHttpWithSubstatusCodeException
    {
        protected GoneException(int subStatusCode)
            : this(subStatusCode, message: null)
        {
        }

        protected GoneException(int subStatusCode, string message)
            : this(subStatusCode, message: message, innerException: null)
        {
        }

        protected GoneException(int subStatusCode, string message, Exception innerException)
            : base(statusCode: HttpStatusCode.Gone, subStatusCode: subStatusCode, message: message, innerException: innerException)
        {
        }
    }
}
