// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.BadRequest
{
    using System;

    internal sealed class UnknownBadRequestException : BadRequestException
    {
        public UnknownBadRequestException(int subStatusCode)
            : this(subStatusCode: subStatusCode, message: null)
        {
        }

        public UnknownBadRequestException(int subStatusCode, string message)
            : this(subStatusCode: subStatusCode, message: message, innerException: null)
        {
        }

        public UnknownBadRequestException(int subStatusCode, string message, Exception innerException)
            : base(subStatusCode: subStatusCode, message: message, innerException: innerException)
        {
        }
    }
}
