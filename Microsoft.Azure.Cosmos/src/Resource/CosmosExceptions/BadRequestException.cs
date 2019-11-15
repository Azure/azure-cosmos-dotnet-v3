// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions
{
    using System;
    using System.Net;

    internal sealed class BadRequestException : CosmosHttpException
    {
        public BadRequestException()
            : base(statusCode: HttpStatusCode.BadRequest, message: null)
        {
        }

        public BadRequestException(string message)
            : base(statusCode: HttpStatusCode.BadRequest, message: message)
        {
        }

        public BadRequestException(string message, Exception innerException)
            : base(statusCode: HttpStatusCode.BadRequest, message: message, innerException: innerException)
        {
        }
    }
}
