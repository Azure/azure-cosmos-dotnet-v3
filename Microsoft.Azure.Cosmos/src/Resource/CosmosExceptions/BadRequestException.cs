// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions
{
    using System;

    internal sealed class BadRequestException : CosmosHttpException
    {
        public BadRequestException()
            : base(statusCode: System.Net.HttpStatusCode.BadRequest, message: null)
        {
        }

        public BadRequestException(string message)
            : base(statusCode: System.Net.HttpStatusCode.BadRequest, message: message)
        {
        }

        public BadRequestException(string message, Exception innerException)
            : base(statusCode: System.Net.HttpStatusCode.BadRequest, message: message, innerException: innerException)
        {
        }
    }
}
