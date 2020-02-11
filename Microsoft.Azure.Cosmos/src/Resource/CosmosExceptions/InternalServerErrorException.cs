// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions
{
    using System;
    using System.Net;

    internal sealed class InternalServerErrorException : CosmosHttpException
    {
        public InternalServerErrorException()
            : base(statusCode: HttpStatusCode.InternalServerError, message: null)
        {
        }

        public InternalServerErrorException(string message)
            : base(statusCode: HttpStatusCode.InternalServerError, message: message)
        {
        }

        public InternalServerErrorException(string message, Exception innerException)
            : base(statusCode: HttpStatusCode.InternalServerError, message: message, innerException: innerException)
        {
        }
    }
}
